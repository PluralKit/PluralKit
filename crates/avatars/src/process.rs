use image::{DynamicImage, ImageFormat};
use std::borrow::Cow;
use std::io::Cursor;
use std::time::Instant;
use tracing::{debug, error, info, instrument};

use crate::{hash::Hash, ImageKind, PKAvatarError};

const MAX_DIMENSION: u32 = 4000;

pub struct ProcessOutput {
    pub width: u32,
    pub height: u32,
    pub hash: Hash,
    pub format: ProcessedFormat,
    pub data: Vec<u8>,
}

#[derive(Copy, Clone, Debug)]
pub enum ProcessedFormat {
    Webp,
    Gif,
}

impl ProcessedFormat {
    pub fn mime_type(&self) -> &'static str {
        match self {
            ProcessedFormat::Gif => "image/gif",
            ProcessedFormat::Webp => "image/webp",
        }
    }

    pub fn extension(&self) -> &'static str {
        match self {
            ProcessedFormat::Webp => "webp",
            ProcessedFormat::Gif => "gif",
        }
    }
}

// Moving Vec<u8> in here since the thread needs ownership of it now, it's fine, don't need it after
pub async fn process_async(data: Vec<u8>, kind: ImageKind) -> Result<ProcessOutput, PKAvatarError> {
    tokio::task::spawn_blocking(move || process(&data, kind))
        .await
        .map_err(|je| PKAvatarError::InternalError(je.into()))?
}

#[instrument(skip_all)]
pub fn process(data: &[u8], kind: ImageKind) -> Result<ProcessOutput, PKAvatarError> {
    let time_before = Instant::now();
    let reader = reader_for(data);
    match reader.format() {
        Some(ImageFormat::Png | ImageFormat::WebP | ImageFormat::Jpeg | ImageFormat::Tiff) => {} // ok :)
        Some(ImageFormat::Gif) => {
            // animated gifs will need to be handled totally differently
            // so split off processing here and come back if it's not applicable
            // (non-banner gifs + 1-frame animated gifs still need to be webp'd)
            if let Some(output) = process_gif(data, kind)? {
                return Ok(output);
            }
        }
        Some(other) => return Err(PKAvatarError::UnsupportedImageFormat(other)),
        None => return Err(PKAvatarError::UnknownImageFormat),
    }

    // want to check dimensions *before* decoding so we don't accidentally end up with a memory bomb
    // eg. a 16000x16000 png file is only 31kb and expands to almost a gig of memory
    let (width, height) = assert_dimensions(reader.into_dimensions()?)?;

    // need to make a new reader??? why can't it just use the same one. reduce duplication?
    let reader = reader_for(data);

    let time_after_parse = Instant::now();

    // apparently `image` sometimes decodes webp images wrong/weird.
    // see: https://discord.com/channels/466707357099884544/667795132971614229/1209925940835262464
    // instead, for webp, we use libwebp itself to decode, as well.
    // (pls no cve)
    let image = if reader.format() == Some(ImageFormat::WebP) {
        let webp_image = webp::Decoder::new(data).decode().ok_or_else(|| {
            PKAvatarError::InternalError(anyhow::anyhow!("webp decode failed").into())
        })?;
        webp_image.to_image()
    } else {
        reader.decode().map_err(|e| {
            // print the ugly error, return the nice error
            error!("error decoding image: {}", e);
            PKAvatarError::ImageFormatError(e)
        })?
    };

    let time_after_decode = Instant::now();
    let image = resize(image, kind);
    let time_after_resize = Instant::now();

    let encoded = encode(image);
    let time_after = Instant::now();

    info!(
        "{}: lossy size {}K (parse: {} ms, decode: {} ms, resize: {} ms, encode: {} ms)",
        encoded.hash,
        encoded.data.len() / 1024,
        (time_after_parse - time_before).as_millis(),
        (time_after_decode - time_after_parse).as_millis(),
        (time_after_resize - time_after_decode).as_millis(),
        (time_after - time_after_resize).as_millis(),
    );

    debug!(
        "processed image {}: {} bytes, {}x{} -> {} bytes, {}x{}",
        encoded.hash,
        data.len(),
        width,
        height,
        encoded.data.len(),
        encoded.width,
        encoded.height
    );
    Ok(encoded)
}

fn assert_dimensions((width, height): (u32, u32)) -> Result<(u32, u32), PKAvatarError> {
    if width > MAX_DIMENSION || height > MAX_DIMENSION {
        return Err(PKAvatarError::ImageDimensionsTooLarge(
            (width, height),
            (MAX_DIMENSION, MAX_DIMENSION),
        ));
    }
    return Ok((width, height));
}
fn process_gif(input_data: &[u8], kind: ImageKind) -> Result<Option<ProcessOutput>, PKAvatarError> {
    // gifs only supported for banners
    if kind != ImageKind::Banner {
        return Ok(None);
    }

    // and we can't rescale gifs (i tried :/) so the max size is the real limit
    if kind != ImageKind::Banner {
        return Ok(None);
    }

    let reader = gif::Decoder::new(Cursor::new(input_data)).map_err(Into::<anyhow::Error>::into)?;
    let (max_width, max_height) = kind.size();
    if reader.width() as u32 > max_width || reader.height() as u32 > max_height {
        return Err(PKAvatarError::ImageDimensionsTooLarge(
            (reader.width() as u32, reader.height() as u32),
            (max_width, max_height),
        ));
    }
    Ok(process_gif_inner(reader).map_err(Into::<anyhow::Error>::into)?)
}

fn process_gif_inner(
    mut reader: gif::Decoder<Cursor<&[u8]>>,
) -> Result<Option<ProcessOutput>, anyhow::Error> {
    let time_before = Instant::now();

    let (width, height) = (reader.width(), reader.height());

    let mut writer = gif::Encoder::new(
        Vec::new(),
        width as u16,
        height as u16,
        reader.global_palette().unwrap_or(&[]),
    )?;
    writer.set_repeat(reader.repeat())?;

    let mut frame_buf = Vec::new();

    let mut frame_count = 0;
    while let Some(frame) = reader.next_frame_info()? {
        let mut frame = frame.clone();
        assert_dimensions((frame.width as u32, frame.height as u32))?;
        frame_buf.clear();
        frame_buf.resize(reader.buffer_size(), 0);
        reader.read_into_buffer(&mut frame_buf)?;
        frame.buffer = Cow::Borrowed(&frame_buf);

        frame.make_lzw_pre_encoded();
        writer.write_lzw_pre_encoded_frame(&frame)?;
        frame_count += 1;
    }

    if frame_count == 1 {
        // If there's only one frame, then this doesn't need to be a gif. webp it
        // (unfortunately we can't tell if there's only one frame until after the first frame's been decoded...)
        return Ok(None);
    }

    let data = writer.into_inner()?;
    let time_after = Instant::now();

    let hash = Hash::sha256(&data);

    let original_data = reader.into_inner();
    info!(
        "processed gif {}: {}K -> {}K ({} ms, frames: {})",
        hash,
        original_data.buffer().len() / 1024,
        data.len() / 1024,
        (time_after - time_before).as_millis(),
        frame_count
    );

    Ok(Some(ProcessOutput {
        data,
        format: ProcessedFormat::Gif,
        hash,
        width: width as u32,
        height: height as u32,
    }))
}

fn reader_for(data: &[u8]) -> image::io::Reader<Cursor<&[u8]>> {
    image::io::Reader::new(Cursor::new(data))
        .with_guessed_format()
        .expect("cursor i/o is infallible")
}

#[instrument(skip_all)]
fn resize(image: DynamicImage, kind: ImageKind) -> DynamicImage {
    let (target_width, target_height) = kind.size();
    if image.width() <= target_width && image.height() <= target_height {
        // don't resize if already smaller
        return image;
    }

    // todo: best filter?
    let resized = image.resize(
        target_width,
        target_height,
        image::imageops::FilterType::Lanczos3,
    );
    return resized;
}

#[instrument(skip_all)]
// can't believe this is infallible
fn encode(image: DynamicImage) -> ProcessOutput {
    let (width, height) = (image.width(), image.height());
    let image_buf = image.to_rgba8();

    let encoded_lossy = webp::Encoder::new(&*image_buf, webp::PixelLayout::Rgba, width, height)
        .encode_simple(false, 90.0)
        .expect("encode should be infallible")
        .to_vec();

    let hash = Hash::sha256(&encoded_lossy);

    ProcessOutput {
        data: encoded_lossy,
        format: ProcessedFormat::Webp,
        hash,
        width,
        height,
    }
}
