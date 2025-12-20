import { error } from '@sveltejs/kit';

const pages = import.meta.glob('/content/**/*.md', { eager: true }) as Record<string, { default: unknown }>;

export async function load({ params }) {
    const slug = params.slug || 'index';

    const page = pages[`/content/${slug}.md`] || pages[`/content/${slug}/index.md`];
    if (!page) {
        throw error(404, `Page not found: ${slug}`);
    }

    return {
        PageContent: page.default
    };
}
