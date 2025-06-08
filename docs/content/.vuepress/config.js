module.exports = {
  title: 'PluralKit',
  theme: 'default-prefers-color-scheme',

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
    }
  },

  themeConfig: {
    repo: 'PluralKit/PluralKit',
    docsDir: 'docs/content/',
    docsBranch: 'main',
    editLinks: true,
    editLinkText: 'Help us improve this page!',
    lastUpdated: "Last updated",
    nextLinks: true,
    prevLinks: true,
    nav: [
      { text: "Web dashboard", link: "https://dash.pluralkit.me" },
      { text: "Support server", link: "https://discord.gg/PczBt78" },
      { text: "Invite bot", link: "https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot%20applications.commands&permissions=536995904" }
    ],
    sidebar: [
      "/",
      ["https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot%20applications.commands&permissions=536995904", "Add to your server"],
      {
        title: "Updates",
        collapsable: false,
        sidebarDepth: 1,
        children: [
          "/posts/",
          "/changelog",
        ]
      },
      {
        title: "Documentation",
        collapsable: false,
        sidebarDepth: 2,
        children: [
          "/getting-started",
          "/user-guide",
          "/command-list",
          "/privacy-policy",
          "/terms-of-service",
          "/faq",
          "/tips-and-tricks"
        ]
      },
      {
        title: "For server staff",
        collapsable: false,
        children: [
          "/staff/permissions",
          "/staff/moderation",
          "/staff/disabling",
          "/staff/logging",
          "/staff/compatibility",
        ]
      },
      {
        title: "API Documentation",
        collapsable: false,
        children: [
          "/api/changelog",
          "/api/reference",
          "/api/endpoints",
          "/api/models",
          "/api/errors",
          "/api/dispatch"
        ]
      },
      ["https://discord.gg/PczBt78", "Join the support server"],
    ],
  },

  plugins: [
    '@vuepress/plugin-back-to-top',
    ["vuepress-plugin-clean-urls", { normalSuffix: "/" }],
  ],

  globalUIComponents: [
    'PluralKitBanner'
  ]
}
