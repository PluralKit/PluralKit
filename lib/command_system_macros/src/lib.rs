use proc_macro2::{Delimiter, TokenStream, TokenTree, Literal, Span};
use syn::parse::{Parse, ParseStream, Result as ParseResult};
use syn::{parse_macro_input, Token, Ident};
use quote::{quote, quote_spanned};

enum CommandToken {
    /// "typed argument" being a member of the `Token` enum in the
    /// command parser crate.
    ///
    /// prefixed with `@` in the command macro.
    TypedArgument(Ident, Span),

    /// interpreted as a literal string in the command input.
    ///
    /// no prefix in the command macro.
    Literal(Literal, Span),
}

impl Parse for CommandToken {
    fn parse(input: ParseStream) -> ParseResult<Self> {
        let lookahead = input.lookahead1();
        if lookahead.peek(Token![@]) {
            // typed argument
            input.parse::<Token![@]>()?;
            let ident = input.parse::<Ident>()?;
            Ok(Self::TypedArgument(ident.clone(), ident.span()))
        } else if lookahead.peek(Ident) {
            // literal string
            let ident = input.parse::<Ident>()?;
            let lit = Literal::string(&format!("{ident}"));
            Ok(Self::Literal(lit, ident.span()))
        } else {
            Err(input.error("expected a command token"))
        }
    }
}

impl Into<TokenStream> for CommandToken {
    fn into(self) -> TokenStream {
        match self {
            Self::TypedArgument(ident, span) => quote_spanned! {span=>
                Token::#ident
            },

            Self::Literal(lit, span) => quote_spanned! {span=>
                Token::Value(vec![ #lit.to_string(), ])
            },
        }.into()
    }
}

struct Command {
    tokens: Vec<CommandToken>,
    help: Literal,
    cb: Literal,
}

impl Parse for Command {
    fn parse(input: ParseStream) -> ParseResult<Self> {
        let mut tokens = Vec::<CommandToken>::new();
        loop {
            if input.peek(Token![,]) {
                break;
            }

            tokens.push(input.parse::<CommandToken>()?);
        }
        input.parse::<Token![,]>()?;

        let cb_ident = input.parse::<Ident>()?;
        let cb = Literal::string(&format!("{cb_ident}"));
        input.parse::<Token![,]>()?;

        let help = input.parse::<Literal>()?;

        Ok(Self {
            tokens,
            cb,
            help,
        })
    }
}

impl Into<TokenStream> for Command {
    fn into(self) -> TokenStream {
        let Self { tokens, help, cb } = self;
        let tokens = tokens.into_iter().map(Into::into).collect::<Vec<TokenStream>>();

        quote! {
            Command { tokens: vec![#(#tokens),*], help: #help.to_string(), cb: #cb.to_string() }
        }
    }
}

#[proc_macro]
pub fn commands(stream: proc_macro::TokenStream) -> proc_macro::TokenStream {
    let stream: TokenStream = stream.into();
    let mut commands: Vec<TokenStream> = Vec::new();

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
                let group_stream: proc_macro::TokenStream = group.stream().into();
                commands.push(parse_macro_input!(group_stream as Command).into());
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
        .map(|v| -> TokenStream { quote! { tree.register_command(#v); }.into() })
        .collect::<TokenStream>();

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
