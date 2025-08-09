use proc_macro2::{Span, TokenStream};
use quote::quote;
use syn::{parse_macro_input, DeriveInput, Expr, Ident, Meta, Type};

#[derive(Clone, Debug)]
enum ElemPatchability {
    None,
    Private,
    Public,
}

#[derive(Clone, Debug)]
struct ModelField {
    name: Ident,
    ty: Type,
    patch: ElemPatchability,
    json: Option<Expr>,
    is_privacy: bool,
    privacy: Option<Expr>,
    default: Option<Expr>,
}

fn parse_field(field: syn::Field) -> ModelField {
    let mut f = ModelField {
        name: field.ident.expect("field missing ident"),
        ty: field.ty,
        patch: ElemPatchability::None,
        json: None,
        is_privacy: false,
        privacy: None,
        default: None,
    };

    for attr in field.attrs.iter() {
        match &attr.meta {
            Meta::Path(path) => {
                let ident = path.segments[0].ident.to_string();
                match ident.as_str() {
                    "private_patchable" => match f.patch {
                        ElemPatchability::None => {
                            f.patch = ElemPatchability::Private;
                        }
                        _ => {
                            panic!("cannot have multiple patch tags on same field");
                        }
                    },
                    "patchable" => match f.patch {
                        ElemPatchability::None => {
                            f.patch = ElemPatchability::Public;
                        }
                        _ => {
                            panic!("cannot have multiple patch tags on same field");
                        }
                    },
                    "privacy" => f.is_privacy = true,
                    _ => panic!("unknown attribute"),
                }
            }
            Meta::NameValue(nv) => match nv.path.segments[0].ident.to_string().as_str() {
                "json" => {
                    if f.json.is_some() {
                        panic!("cannot set json multiple times for same field");
                    }
                    f.json = Some(nv.value.clone());
                }
                "privacy" => {
                    if f.privacy.is_some() {
                        panic!("cannot set privacy multiple times for same field");
                    }
                    f.privacy = Some(nv.value.clone());
                }
                "default" => {
                    if f.default.is_some() {
                        panic!("cannot set default multiple times for same field");
                    }
                    f.default = Some(nv.value.clone());
                }
                _ => panic!("unknown attribute"),
            },
            Meta::List(_) => panic!("unknown attribute"),
        }
    }

    if matches!(f.patch, ElemPatchability::Public) && f.json.is_none() {
        panic!("must have json name to be publicly patchable");
    }

    if f.json.is_some() && f.is_privacy {
        panic!("cannot set custom json name for privacy field");
    }

    f
}

pub fn macro_impl(
    _args: proc_macro::TokenStream,
    input: proc_macro::TokenStream,
) -> proc_macro::TokenStream {
    let ast = parse_macro_input!(input as DeriveInput);
    let model_type = match ast.data {
        syn::Data::Struct(struct_data) => struct_data,
        _ => panic!("pk_model can only be used on a struct"),
    };

    let tname = Ident::new(&format!("PK{}", ast.ident), Span::call_site());
    let patchable_name = Ident::new(&format!("PK{}Patch", ast.ident), Span::call_site());

    let fields = if let syn::Fields::Named(fields) = model_type.fields {
        fields
            .named
            .iter()
            .map(|f| parse_field(f.clone()))
            .collect::<Vec<ModelField>>()
    } else {
        panic!("fields of a struct must be named");
    };

    let tfields = mk_tfields(fields.clone());
    let from_json = mk_tfrom_json(fields.clone());
    let _from_sql = mk_tfrom_sql(fields.clone());
    let to_json = mk_tto_json(fields.clone());

    let fields: Vec<ModelField> = fields
        .iter()
        .filter(|f| !matches!(f.patch, ElemPatchability::None))
        .cloned()
        .collect();

    let patch_fields = mk_patch_fields(fields.clone());
    let patch_from_json = mk_patch_from_json(fields.clone());
    let patch_validate = mk_patch_validate(fields.clone());
    let patch_to_json = mk_patch_to_json(fields.clone());
    let patch_to_sql = mk_patch_to_sql(fields.clone());

    return quote! {
        #[derive(sqlx::FromRow, Debug, Clone)]
        pub struct #tname {
            #tfields
        }

        impl #tname {
            pub fn from_json(input: String) -> Self {
                #from_json
            }

            #to_json
        }

        #[derive(Debug, Clone)]
        pub struct #patchable_name {
            #patch_fields
        }

        impl #patchable_name {
            pub fn from_json(input: String) -> Self {
                #patch_from_json
            }

            pub fn validate(self) -> bool {
                #patch_validate
            }

            pub fn to_sql(self) -> sea_query::UpdateStatement {
                // sea_query::Query::update()
                    #patch_to_sql
            }

            pub fn to_json(self) -> serde_json::Value {
                #patch_to_json
            }
        }
    }
    .into();
}

fn mk_tfields(fields: Vec<ModelField>) -> TokenStream {
    fields
        .iter()
        .map(|f| {
            let name = f.name.clone();
            let ty = f.ty.clone();
            quote! {
                pub #name: #ty,
            }
        })
        .collect()
}
fn mk_tfrom_json(_fields: Vec<ModelField>) -> TokenStream {
    quote! { unimplemented!(); }
}
fn mk_tfrom_sql(_fields: Vec<ModelField>) -> TokenStream {
    quote! { unimplemented!(); }
}
fn mk_tto_json(fields: Vec<ModelField>) -> TokenStream {
    let has_privacy = fields.iter().any(|f| f.privacy.is_some());
    let fielddefs: TokenStream = fields
        .iter()
        .filter_map(|f| {
            f.json.as_ref().map(|v| {
                let tname = f.name.clone();
                let maybepriv = if let Some(privacy) = f.privacy.as_ref() {
                    quote! {
                        #v: crate::_util::privacy_lookup!(self.#tname, self.#privacy, lookup_level)
                    }
                } else {
                    quote! {
                        #v: self.#tname
                    }
                };
                if let Some(default) = f.default.as_ref() {
                    quote! {
                        #maybepriv.unwrap_or(#default),
                    }
                } else {
                    quote! {
                        #maybepriv,
                    }
                }
            })
        })
        .collect();

    let privacyfielddefs: TokenStream = fields
        .iter()
        .filter_map(|f| {
            if f.is_privacy {
                let tname = f.name.clone();
                let tnamestr = f.name.clone().to_string();
                Some(quote! {
                    #tnamestr: self.#tname,
                })
            } else {
                None
            }
        })
        .collect();

    let privdef = if has_privacy {
        quote! {
            , lookup_level: crate::PrivacyLevel
        }
    } else {
        quote! {}
    };

    let privacy_fielddefs = if has_privacy {
        quote! {
            "privacy": if matches!(lookup_level, crate::PrivacyLevel::Private) {
                Some(serde_json::json!({
                    #privacyfielddefs
                }))
            } else {
                None
            }
        }
    } else {
        quote! {}
    };

    quote! {
        pub fn to_json(self #privdef) -> serde_json::Value {
            serde_json::json!({
                #fielddefs
                #privacy_fielddefs
            })
        }
    }
}

fn mk_patch_fields(fields: Vec<ModelField>) -> TokenStream {
    fields
        .iter()
        .map(|f| {
            let name = f.name.clone();
            let ty = f.ty.clone();
            quote! {
                pub #name: Option<#ty>,
            }
        })
        .collect()
}
fn mk_patch_validate(_fields: Vec<ModelField>) -> TokenStream {
    quote! { true }
}
fn mk_patch_from_json(_fields: Vec<ModelField>) -> TokenStream {
    quote! { unimplemented!(); }
}
fn mk_patch_to_sql(_fields: Vec<ModelField>) -> TokenStream {
    quote! { unimplemented!(); }
}
fn mk_patch_to_json(_fields: Vec<ModelField>) -> TokenStream {
    quote! { unimplemented!(); }
}
