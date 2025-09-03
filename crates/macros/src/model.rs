use proc_macro2::{Span, TokenStream};
use quote::quote;
use syn::{DeriveInput, Expr, Ident, Meta, Type, parse_macro_input};

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

    if f.is_privacy && f.json.is_none() {
        f.json = Some(syn::Expr::Lit(syn::ExprLit {
            attrs: vec![],
            lit: syn::Lit::Str(syn::LitStr::new(
                f.name.clone().to_string().as_str(),
                proc_macro2::Span::call_site(),
            )),
        }))
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
        .filter(|f| f.is_privacy || !matches!(f.patch, ElemPatchability::None))
        .cloned()
        .collect();

    let patch_fields = mk_patch_fields(fields.clone());
    let patch_validate = mk_patch_validate(fields.clone());
    let patch_validate_bulk = mk_patch_validate_bulk(fields.clone());
    let patch_to_json = mk_patch_to_json(fields.clone());
    let patch_to_sql = mk_patch_to_sql(fields.clone());

    let code = quote! {
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

        #[derive(Debug, Clone, Default)]
        pub struct #patchable_name {
            #patch_fields

            errors: Vec<crate::ValidationError>,
        }

        impl #patchable_name {
            pub fn validate(&mut self) {
                #patch_validate
            }

            pub fn errors(&self) -> Vec<crate::ValidationError> {
                self.errors.clone()
            }

            pub fn validate_bulk(&mut self) {
                #patch_validate_bulk
            }

            pub fn to_sql(self) -> sea_query::UpdateStatement {
                use sea_query::types::*;
                let mut patch = &mut sea_query::Query::update();
                #patch_to_sql
                patch.clone()
            }

            pub fn to_json(self) -> serde_json::Value {
                #patch_to_json
            }
        }
    };

    // panic!("{:#?}", code.to_string());

    return code.into();
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
                let tnamestr = f.json.clone();
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
    quote! { unimplemented!(); }
}
fn mk_patch_validate_bulk(fields: Vec<ModelField>) -> TokenStream {
    // iterate over all nullable patchable fields other than privacy
    // add an error if any field is set to a value other than null
    fields
        .iter()
        .map(|f| {
            if let syn::Type::Path(path) = &f.ty && let Some(inner) = path.path.segments.last() && inner.ident != "Option" {
                return quote! {};
            }
            let name = f.name.clone();
            if matches!(f.patch, ElemPatchability::Public) {
                let json = f.json.clone().unwrap();
                quote! {
                    if let Some(val) = self.#name.clone() && val.is_some() {
                        self.errors.push(ValidationError::simple(#json, "Only null values are supported in bulk endpoint"));
                    }
                }
            } else {
                quote! {}
            }
        })
        .collect()
}
fn mk_patch_to_sql(fields: Vec<ModelField>) -> TokenStream {
    fields
        .iter()
        .filter_map(|f| {
            if !matches!(f.patch, ElemPatchability::None) || f.is_privacy {
                let name = f.name.clone();
                let column = f.name.to_string();
                Some(quote! {
                    if let Some(value) = self.#name {
                        patch = patch.value(#column, value);
                    }
                })
            } else {
                None
            }
        })
        .collect()
}
fn mk_patch_to_json(_fields: Vec<ModelField>) -> TokenStream {
    quote! { unimplemented!(); }
}
