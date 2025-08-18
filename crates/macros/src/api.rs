use quote::quote;
use syn::{parse_macro_input, FnArg, ItemFn, Pat};

fn pretty_print(ts: &proc_macro2::TokenStream) -> String {
    let file = syn::parse_file(&ts.to_string()).unwrap();
    prettyplease::unparse(&file)
}

pub fn macro_impl(
    _args: proc_macro::TokenStream,
    input: proc_macro::TokenStream,
    is_internal: bool,
) -> proc_macro::TokenStream {
    let input = parse_macro_input!(input as ItemFn);

    let fn_name = &input.sig.ident;
    let fn_params = &input.sig.inputs;
    let fn_body = &input.block;
    let syn::ReturnType::Type(_, fn_return_type) = &input.sig.output else {
        panic!("handler return type must not be nothing");
    };
    let pms: Vec<proc_macro2::TokenStream> = fn_params
        .iter()
        .map(|v| {
            let FnArg::Typed(pat) = v else {
                panic!("must not have self param in handler");
            };
            let mut pat = pat.pat.clone();
            if let Pat::Ident(ident) = *pat {
                let mut ident = ident.clone();
                ident.mutability = None;
                pat = Box::new(Pat::Ident(ident));
            }
            quote! { #pat }
        })
        .collect();

    let internal_res = if is_internal {
        quote! {
            if !auth.internal() {
                return crate::error::FORBIDDEN_INTERNAL_ROUTE.into_response();
            }
        }
    } else {
        quote!()
    };

    let res = quote! {
        #[allow(unused_mut)]
        pub async fn #fn_name(#fn_params) -> axum::response::Response {
            async fn inner(#fn_params) -> Result<#fn_return_type, crate::error::PKError> {
                #fn_body
            }

            #internal_res
            match inner(#(#pms),*).await {
                Ok(res) => res.into_response(),
                Err(err) => err.into_response(),
            }
        }
    };

    res.into()
}
