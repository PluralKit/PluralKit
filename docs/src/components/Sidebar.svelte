<script lang="ts">
	import { page } from "$app/stores";

	const mdModules = import.meta.glob('/content/**/*.md', { eager: true }) as Record<string, { metadata?: { title?: string; permalink?: string } }>;

	const pathToTitle: Record<string, string> = {};
	for (const [filePath, mod] of Object.entries(mdModules)) {
		const urlPath = filePath
			.replace('/content', '')
			.replace(/\/index\.md$/, '')
			.replace(/\.md$/, '');

		if (mod.metadata?.title) {
			pathToTitle[urlPath || '/'] = mod.metadata.title;
		}
	}

	function getTitle(path: string): string {
		return pathToTitle[path] || path.split('/').pop() || path;
	}

	const sidebar = [
		{
			title: "Home",
			href: "/",
		},
		{
			title: "Add to your server",
			href: "https://discord.com/oauth2/authorize?client_id=466378653216014359&scope=bot%20applications.commands&permissions=536995904",
		},
		{
			title: "Updates",
			sidebarDepth: 1,
			children: [
				"/posts",
				"/changelog",
			]
		},
		{
			title: "Documentation",
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
			children: [
				"/api/changelog",
				"/api/reference",
				"/api/endpoints",
				"/api/models",
				"/api/errors",
				"/api/dispatch"
			]
		},
		{
			title: "Join the support server",
			href: "https://discord.gg/PczBt78",
		},
		];

	function isActive(href: string): boolean {
		return $page.url.pathname === href;
	}
	</script>

<aside class="w-80 bg-base-200 p-4 overflow-y-auto shrink-0 min-h-0">
	<ul class="menu w-full">
		{#each sidebar as item}
			{#if item.children}
				<li class="menu-title flex flex-row items-center gap-2 mt-4">
					{item.title}
				</li>
				{#each item.children as child}
					<li>
						<a href={child} class:active={isActive(child)}>
						{getTitle(child)}
						</a>
					</li>
				{/each}
			{:else}
				<li>
					<a href={item.href} class:active={isActive(item.href)}>
						{item.title}
					</a>
				</li>
			{/if}
		{/each}
	</ul>
</aside>
