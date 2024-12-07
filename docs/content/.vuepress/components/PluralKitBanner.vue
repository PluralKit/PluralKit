<template>
  <aside class="pkBanner" v-if="shouldShowBanner">
    <div class="pkBannerText" v-html="bannerContent"></div>
  </aside>
</template>

<script>
  export default {
    name: 'PluralKitBanner',
    computed: {
      shouldShowBanner() {
        return typeof this.$site.themeConfig.pkBannerContent === "string"
      },
      bannerContent() {
        return this.$site.themeConfig.pkBannerContent
      },
    },
    beforeMount() {
      if (this.shouldShowBanner) {
        document.getElementsByTagName('html')[0].classList.add("pkBannerVisible")
      }
    },
  }
</script>

<style lang="stylus">
  html.pkBannerVisible .theme-container
    position relative
    margin-top 3rem

    .navbar
      top 3rem

  .pkBanner
    position fixed
    top 0
    left 0
    right 0
    height 3rem
    background var(--bgColor)

    .pkBannerText
      padding 0.5rem 1rem
      min-width 100%
      height 3rem

      background var(--warningBgColor)
      color var(--textColor)

      line-height 2rem
      overflow auto hidden
      white-space nowrap
      word-break keep-all
      text-align center
</style>
