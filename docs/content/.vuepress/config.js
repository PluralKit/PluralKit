module.exports = {
    title: "PluralKit",
    
    base: "/",
    head: [
        ["link", { rel: "icon", type: "image/png", href: "/favicon.png" }],
    ],
    evergreen: true,
    
    theme: "@vuepress/theme-default",
    plugins: [
        ["vuepress-plugin-clean-urls", {normalSuffix: "/"}],
    ],
    markdown: {
        extendMarkdown: md => {
            md.use(require("markdown-it-custom-header-link"));
            md.use(require("markdown-it-imsize"));
        }
    },
    
    themeConfig: {
        nav: [
            { text: "THIS IS A WORK IN PROGRESS SITE. PLEASE SEE THE OFFICIAL DOCUMENTATION FOR COMPLETE INFORMATION.", link: "https://pluralkit.me" },
            { text: "Support server", link: "https://discord.gg/PczBt78" },
            { text: "Invite bot", link: "https://discordapp.com/oauth2/authorize?client_id=466378653216014359&scope=bot&permissions=536995904" }
        ],
        sidebar: [
            ["/", "Home"],
            {
                title: "User Guide",
                collapsable: false,
                children: [
                    "/guide/quick-start",
                    "/guide/ids",
                    "/guide/listing",
                    "/guide/systems",
                    "/guide/members",
                    "/guide/proxying",
                    "/guide/moderation",
                    "/guide/privacy",
                    "/guide/commands",
                ]
            },
            "/faq",
            "/api",
            "/privacy-policy",
            "/support-server",
        ],
        lastUpdated: "Last Updated",
        
        repo: "xSke/PluralKit",
        repoLabel: "Contribute!",
        docsDir: "docs/content",
        docsBranch: "new-docs",
        editLinks: true,
        editLinkText: "Help us improve this page!"
    }
}