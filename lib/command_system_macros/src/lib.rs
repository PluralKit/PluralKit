use proc_macro::{Delimiter, TokenStream, TokenTree};
use proc_macro2::{Ident, Literal, Span};
use quote::quote;

fn make_command(
    tokens: Vec<proc_macro2::TokenStream>,
    help: String,
    cb: String,
) -> proc_macro2::TokenStream {
    let help = Literal::string(&help);
    let cb = Literal::string(&cb);

    quote! {
        Command { tokens: vec![#(#tokens),*], help: #help.to_string(), cb: #cb.to_string() }
    }
}

// horrible, but the best way i could find to do this
fn token_to_string(i: String) -> String {
    i.to_string()[1..i.to_string().len() - 1].to_string()
}

fn command_from_stream(stream: TokenStream) -> proc_macro2::TokenStream {
    let mut part = 0;
    let mut found_tokens: Vec<proc_macro2::TokenStream> = Vec::new();
    let mut found_cb: Option<String> = None;
    let mut found_help: Option<String> = None;

    let mut is_token_lit = false;
    let mut tokens = stream.clone().into_iter();
    'a: loop {
        let cur_token = tokens.next();
        match cur_token {
            None if part == 2 && found_help.is_some() => break 'a,
            Some(TokenTree::Ident(ident)) if part == 0 => {
                found_tokens.push(if is_token_lit {
                    let ident = Ident::new(ident.to_string().as_str(), Span::call_site());
                    quote! { Token::#ident }.into()
                } else {
                    let ident = Literal::string(format!("{ident}").as_str());
                    quote! { Token::Value(vec![#ident.to_string() ]) }
                });
                // reset this
                is_token_lit = false;
            }
            Some(TokenTree::Punct(punct)) if part == 0 && format!("{punct}") == "@" => {
                is_token_lit = true
            }
            Some(TokenTree::Punct(punct))
                if ((part == 0 && found_tokens.len() > 0) || (part == 1 && found_cb.is_some()))
                    && format!("{punct}") == "," =>
            {
                part += 1
            }
            Some(TokenTree::Ident(ident)) if part == 1 => found_cb = Some(format!("{ident}")),
            Some(TokenTree::Literal(lit)) if part == 2 => {
                found_help = Some(token_to_string(lit.to_string()))
            }
            _ => panic!("invalid command definition: {stream}"),
        }
    }
    make_command(found_tokens, found_help.unwrap(), found_cb.unwrap())
}

#[proc_macro]
pub fn commands(stream: TokenStream) -> TokenStream {
    let mut commands: Vec<proc_macro2::TokenStream> = Vec::new();

    let mut top_level_tokens = stream.into_iter();
    'a: loop {
        // "command"
        match top_level_tokens.next() {
            Some(TokenTree::Ident(ident)) if format!("{ident}") == "command" => {}
            None => break 'a,
            _ => panic!("contents of commands! macro is invalid"),
        }
        //
        match top_level_tokens.next() {
            Some(TokenTree::Group(group)) if group.delimiter() == Delimiter::Parenthesis => {
                commands.push(command_from_stream(group.stream()));
            }
            _ => panic!("contents of commands! macro is invalid"),
        }
        // ;
        match top_level_tokens.next() {
            Some(TokenTree::Punct(punct)) if format!("{punct}") == ";" => {}
            _ => panic!("contents of commands! macro is invalid"),
        }
    }

    let command_registrations = commands
        .iter()
        .map(|v| -> proc_macro2::TokenStream { quote! { tree.register_command(#v); }.into() })
        .collect::<proc_macro2::TokenStream>();

    let res = quote! {
        lazy_static::lazy_static! {
            static ref COMMAND_TREE: TreeBranch = {
                let mut tree = TreeBranch {
                    current_command_key: None,
                    possible_tokens: vec![],
                    branches: HashMap::new(),
                };

                #command_registrations

                tree.sort_tokens();

                // println!("{{tree:#?}}");

                tree
            };
        }
    };

    // panic!("{res}");

    res.into()
}
