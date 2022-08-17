const discordCDNAttachmentRegex =
  /^https:\/\/cdn\.discordapp\.com\/attachments\/([^?]+)/i

const resizeMedia = (
  mediaURL: string,
  dimensions?: number[],
  format?: string,
) =>
  mediaURL.replace(
    discordCDNAttachmentRegex,
    `https://media.discordapp.net/attachments/$1?width=${
      dimensions?.[0] ?? 256
    }&height=${dimensions?.[1] ?? dimensions?.[0] ?? 256}&format=${
      format ?? 'webp'
    }`,
  )

export default resizeMedia
