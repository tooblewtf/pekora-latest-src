<script lang="ts">
	import { link } from "svelte-routing";
	import { HomeIcon, UsersIcon, BookOpenIcon, PlusCircleIcon, EditIcon, FilePlusIcon, CopyIcon, FolderPlusIcon, RefreshCcwIcon, UploadCloudIcon, CheckSquareIcon, SunriseIcon, FlagIcon,StarIcon, BookIcon, PhoneIcon, ActivityIcon, TabletIcon, TargetIcon, TerminalIcon } from "svelte-feather-icons";

	import SavedPages, { addPage } from "../../stores/saved-pages";
	import PageEntry from "../saved-pages/PageEntry.svelte";
	import * as rank from "../../stores/rank";
	let savedPages: { url: string; title: string }[];
	SavedPages.subscribe((v) => (savedPages = v));

	const navItems = [
		{
			name: "Dashboard",
			link: "/admin/",
			icon: HomeIcon,
		},
		{
			name: "Players",
			link: "/admin/players",
			icon: UsersIcon,
			permission: "GetUsersList",
		},
		{
			name: 'Groups',
			link: '/admin/groups',
			icon: ActivityIcon,
			permission: 'GetGroupManageInfo',
		},
		{
			name: 'Game History',
			link: '/admin/game-history',
			icon: TabletIcon,
			permission: 'GetUsersInGame',
		},
		{
			name: "Logs",
			link: "/admin/logs",
			icon: BookOpenIcon,
			permission: "GetAdminLogs",
		},
		{
			name: "Create Player",
			link: "/admin/user/create",
			icon: UsersIcon,
			permission: "CreateUser",
		},
		{
			name: 'Text Moderation',
			link: '/admin/text-posts',
			icon: PhoneIcon,
			permission: 'GetAllAssetComments',
		},
		{
			name: 'Forums',
			link: '/admin/forums',
			icon: BookIcon,
			permission: 'LockForumThread',
		},
		{
			name: "Asset Moderation",
			link: "/admin/asset/approval",
			icon: CheckSquareIcon,
			permission: "GetPendingModerationItems",
		},
		{
			name: "Lottery",
			link: "/admin/lottery",
			icon: SunriseIcon,
			permission: "RunLottery",
		},
		{
			name: "Feature Flags",
			link: "/admin/feature-flags",
			icon: FlagIcon,
			permission: "ManageFeatureFlags",
		},
		{
			name: 'Applications',
			link: '/admin/applications',
			icon: TerminalIcon,
			permission: 'ManageApplications',
		},
		{
			name: 'Force Application',
			link: '/admin/force-application',
			icon: TerminalIcon,
			permission: 'ForceApplication',
		},
		{
			name: 'Reports',
			link: '/admin/reports',
			icon: StarIcon,
			permission: 'ManageReports',
		},
		{
			name: 'Resolve URL',
			link: '/admin/resolve-url',
			icon: TargetIcon,
			permission: 'GetDetailsFromThumbnail',
		},
	] as any[];

	let active: string = "/";
</script>

<div class="row">
	<nav id="sidebarMenu" class="col-md-3 col-lg-2 d-md-block bg-dark text-white sidebar collapse">
		<div class="position-sticky pt-3">
			<h6 class="sidebar-heading d-flex justify-content-between align-items-center px-3 mt-0 mb-1 text-muted">
				<span>Saved pages</span>
				<a
					class="link-secondary"
					href="#!"
					on:click={(e) => {
						e.preventDefault();
						let title = document.title;
						if (title === "Admin" || !title) {
							title = "Untitled Page";
						}
						let url = location.pathname + (location.search || "");
						addPage(title, url);
					}}
				>
					<PlusCircleIcon />
				</a>
			</h6>
			<ul class="nav flex-column mb-2">
				{#if savedPages.length === 0}
					<ul class="nav flex-column mb-0">
						<li class="nav-item">
							<p class="pr-4 pl-4">You do not have any saved pages. Click the <PlusCircleIcon /> icon to save a page.</p>
						</li>
					</ul>
				{:else}
					{#each savedPages as p}
						<PageEntry title={p.title} url={p.url} />
					{/each}
				{/if}
			</ul>
			<ul class="nav flex-column">
				{#each navItems as item}
					{#if !item.permission || rank.hasPermission(item.permission)}
						<li class="nav-item">
							<a use:link class={`nav-link${item.link === active ? " active" : ""}`} href={item.link}>
								<svelte:component this={item.icon} />
								{item.name}
							</a>
						</li>
					{/if}
				{/each}
				{#if rank.hasPermission("CreateAsset") || rank.hasPermission("SetAssetProduct") || rank.hasPermission("MigrateAssetFromRoblox") || rank.hasPermission("CreateAssetVersion") || rank.hasPermission("RequestAssetReRender") || rank.hasPermission('CreateBundleCopiedFromRoblox') || rank.hasPermission('CreateAssetCopiedFromRoblox')}
					<h6 class="sidebar-heading d-flex justify-content-between align-items-center px-3 mt-2 mb-1 text-muted">
						<span style="color: white;">Catalog</span>
					</h6>
				{/if}
				{#if rank.hasPermission("CreateAsset")}
					<li class="nav-item ml-4">
						<a use:link class="nav-link" href="/admin/asset/create"><FilePlusIcon /> Create Item</a>
					</li>
				{/if}
				{#if rank.hasPermission("CreateAssetCopiedFromRoblox")}
					<li class="nav-item ml-4">
						<a use:link class="nav-link" href="/admin/asset/copy"><CopyIcon /> Copy Asset</a>
					</li>
				{/if}
				{#if rank.hasPermission("CreateAssetCopiedFromRoblox")}
					<li class="nav-item ml-4">
						<a use:link class="nav-link" href="/admin/asset/ugccopy"><CopyIcon /> Copy UGC</a>
					</li>
				{/if}
				{#if rank.hasPermission("CreateBundleCopiedFromRoblox")}
					<li class="nav-item ml-4">
						<a use:link class="nav-link" href="/admin/bundle/copy"><CopyIcon /> Copy Bundle</a>
					</li>
				{/if}
				{#if rank.hasPermission("SetAssetProduct")}
					<li class="nav-item ml-4">
						<a use:link class="nav-link" href="/admin/product/update"><EditIcon /> Update Item Product</a>
					</li>
				{/if}

				{#if rank.hasPermission("MigrateAssetFromRoblox")}
					<li class="nav-item ml-4">
						<a use:link class="nav-link" href="/admin/asset/create-for-item"><FolderPlusIcon /> Create Item Asset</a>
					</li>
				{/if}
				{#if rank.hasPermission("CreateAssetVersion")}
					<li class="nav-item ml-4">
						<a use:link class="nav-link" href="/admin/asset/version/create"><UploadCloudIcon /> Update Item RBXM</a>
					</li>
					<li class="nav-item ml-4">
						<a use:link class="nav-link" href="/admin/giftallusers"><FolderPlusIcon /> Gift All Users</a>
					</li>
				{/if}
				{#if rank.hasPermission("GiveUserItem")}
					<li class="nav-item ml-4">
						<a use:link class="nav-link" href="/admin/asset/track"><RefreshCcwIcon /> Tack User Assets</a>
					</li>
				{/if}
				{#if rank.hasPermission("RequestAssetReRender")}
					<li class="nav-item ml-4">
						<a use:link class="nav-link" href="/admin/asset/re-render"><RefreshCcwIcon /> Force Item Re-Render</a>
					</li>
				{/if}
				<li class="nav-item mt-2 d-md-none d-block">
					<a class="nav-link" href="/">Back to Marine</a>
				</li>
			</ul>
		</div>
	</nav>
</div>

<style>
	nav#sidebarMenu.sidebar {
		top: 0 !important;
	}
</style>
