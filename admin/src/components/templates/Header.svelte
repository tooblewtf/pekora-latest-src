<script lang="ts">
	import { onMount } from 'svelte';

	export let title: string;

	onMount(() => {
		document.getElementById('toggle-navbar').onclick = function (ev) {
			ev.preventDefault();
			let toToggle = document.getElementById('sidebarMenu');
			if (toToggle.getAttribute('style')) {
				toToggle.removeAttribute('style');
			} else {
				toToggle.setAttribute('style', 'display: block!important');
			}
		};
	});
</script>

<svelte:head>
	<meta charset="utf-8" />
	<meta name="viewport" content="width=device-width, initial-scale=1" />
	<title>{title || 'Admin'}</title>
</svelte:head>

<header class="navbar navbar-dark sticky-top flex-md-nowrap p-0 shadow">
	<a class="navbar-brand col-md-3 col-lg-2 me-0 px-3" href="/admin">
		Management
	</a>
	<button
		class="navbar-toggler position-absolute d-md-none collapsed"
		type="button"
		id="toggle-navbar"
	>
		<span class="navbar-toggler-icon" />
	</button>
	<ul class="navbar-nav px-3 d-none d-md-block">
		<li class="nav-item text-nowrap">
			<a class="nav-link" href="/">Back to Marine</a>
		</li>
	</ul>
</header>
