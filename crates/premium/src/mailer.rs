use lazy_static::lazy_static;
use postmark::{
    Query,
    api::{Body, email::SendEmailRequest},
    reqwest::PostmarkClient,
};

lazy_static! {
    pub static ref CLIENT: PostmarkClient = {
        PostmarkClient::builder()
            .server_token(&libpk::config.premium().postmark_token)
            .build()
    };
}

const LOGIN_TEXT: &'static str = r#"Hello,

Someone (hopefully you) has requested a link to log in to the PluralKit Premium website.

Click here to log in: {link}

This link will expire in 10 minutes.

If you did not request this link, please ignore this message.

Thanks,
- PluralKit Team
"#;

pub async fn login_token(rcpt: String, token: String) -> anyhow::Result<()> {
    SendEmailRequest::builder()
        .from(&libpk::config.premium().from_email)
        .to(rcpt)
        .subject("[PluralKit Premium] Your login link")
        .body(Body::text(LOGIN_TEXT.replace(
            "{link}",
            format!("{}/login/{token}", libpk::config.premium().base_url).as_str(),
        )))
        .build()
        .execute(&(CLIENT.to_owned()))
        .await?;

    Ok(())
}
