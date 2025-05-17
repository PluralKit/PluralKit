use proc_macro::{Delimiter, TokenTree};
use quote::quote;

pub fn macro_impl(
    _args: proc_macro::TokenStream,
    input: proc_macro::TokenStream,
) -> proc_macro::TokenStream {
    // yes, this ignores everything except the codeblock
    // it's fine.
    let body = match input.into_iter().last().expect("empty") {
        TokenTree::Group(group) if group.delimiter() == Delimiter::Brace => group.stream(),
        _ => panic!("invalid function"),
    };

    let body = proc_macro2::TokenStream::from(body);

    return quote! {
        fn main() {
            let _sentry_guard = libpk::init_sentry();
            libpk::init_logging(env!("CARGO_CRATE_NAME"));
            tokio::runtime::Builder::new_multi_thread()
                .enable_all()
                .build()
                .unwrap()
                .block_on(async {
                    if let Err(error) = libpk::init_metrics() {
                        tracing::error!(?error, "failed to init metrics collector");
                    };

                    tracing::info!("hello world");

                    let result: anyhow::Result<()> = async { #body }.await;

                    if let Err(error) = result {
                        tracing::error!(?error, "failed to run service");
                    };
                });
        }
    }
    .into();
}
