use proc_macro::TokenStream;

mod entrypoint;
mod model;

#[proc_macro_attribute]
pub fn main(args: TokenStream, input: TokenStream) -> TokenStream {
    entrypoint::macro_impl(args, input)
}

#[proc_macro_attribute]
pub fn pk_model(args: TokenStream, input: TokenStream) -> TokenStream {
    model::macro_impl(args, input)
}
