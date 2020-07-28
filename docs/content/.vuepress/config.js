module.exports = {
  title: 'PluralKit',

  base: "/",
  head: [
    ["link", { rel: "icon", type: "image/png", href: "/favicon.png" }],
    ['meta', { name: 'theme-color', content: '#da9317' }],
    ['meta', { name: 'apple-mobile-web-app-capable', content: 'yes' }],
    ['meta', { name: 'apple-mobile-web-app-status-bar-style', content: 'black' }]
  ],
  evergreen: true,

  markdown: {
    extendMarkdown: md => {
      md.use(require("markdown-it-custom-header-link"));
      md.use(require("markdown-it-imsize"));
    }
  },

  themeConfig: {
    repo: 'xSke/PluralKit',
    docsDir: 'docs',
    docsBranch: 'main',
    editLinks: true,
    editLinkText: 'Help us improve this page!',
    lastUpdated: "Last updated",
    nextLinks: false,
    prevLinks: false,
    nav: [
      { text: "Support server", link: "https://discord.gg/PczBt78" },
      { text: "Invite bot", link: "https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot&permissions=536995904" }
    ],
    sidebar: [
      "/",
      {
        title: "Documentation",
        collapsable: false,
        sidebarDepth: 2,
        children: [
          "/getting-started",
          "/user-guide",
          "/command-list",
          "/api-documentation",
          "/privacy-policy",
          "/faq",
          "/tips-and-tricks"
        ]
      },
      ["https://discord.gg/PczBt78", "Join the support server"],
      ["https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot&permissions=536995904", "Add to your server"],
    ]
  },

  plugins: [
    '@vuepress/plugin-back-to-top',
    '@vuepress/plugin-medium-zoom',
    [
      '@vuepress/google-analytics',
      {
        "ga": "UA-173942267-1"
      }
    ],
    ["vuepress-plugin-clean-urls", { normalSuffix: "/" }],
  ],
}
