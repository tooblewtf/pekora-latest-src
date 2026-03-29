<script lang="ts">
	import { navigate } from "svelte-routing";

	import Main from "../components/templates/Main.svelte";
	import request from "../lib/request";
	const itemRbFooter = `We realize you may not have been aware of how these items were originally made available and in such cases, no further moderation action will be placed on your account. However, please be aware that any willful participation in account theft, phishing, or other serious Terms of Service violations, will result in the termination of the associated accounts.
Thank you,

-The Marine Team`;
	export let userId: string;
	const autofill: { name: string; subject: string; placeholders: string[]; message: string }[] = [
		{
			name: "Removed and Added Items",
			subject: "Inventory Adjustment",
			placeholders: ["[Item Name Here]", "[Item Granted]"],
			message: `Hello,

The following item has been removed from your inventory:

[Item Name Here]

We have granted [Item Granted] to your account.

${itemRbFooter}`,
		},
		{
			name: "Removed Items (Compromised)",
			subject: "Inventory Adjustment",
			placeholders: ["[Item Names Here]", "[Item Granted]"],
			message: `Hello,

The following items or currency were taken from a compromised account and have been removed from your inventory:

[Item Names Here]

${itemRbFooter}`,
		},
		{
			name: "Won Giveaway",
			subject: "Giveaway Award",
			placeholders: ["[Item Name or Robux Amount Here]"],
			message: `Hello,

Congratulations on winning our giveaway! You have been awarded with the following prize:

[Item Name or Robux Amount Here]

The prize has already been awarded to your account, so you do not have to do anything to claim it.

Thank you,

- The Marine Team`,
		},
	];

	let disabled = false;
	let errorMessage: string | undefined;
	let allPlaceholders: string[] = [];
	for (const item of autofill) {
		for (const p of item.placeholders) {
			allPlaceholders.push(p);
		}
	}
</script>

<style>
	p.err {
		color: red;
	}
</style>

<svelte:head>
	<title>Message User</title>
</svelte:head>

<Main>
	<div class="row">
		<div class="col-12">
			<h1>Message User</h1>
			{#if errorMessage}
				<p class="err">{errorMessage}</p>
			{/if}
		</div>
		<div class="col-12">
			<label for="message-subject">Subject</label>
			<input type="text" class="form-control dark-input" id="message-subject" {disabled} />
			<label for="message-body">Body</label>
			<textarea id="message-body" class="form-control" rows={12} {disabled} />
		</div>
		<div class="col-12">
			<h3>Autofill</h3>
			<div>
				<div class="btn-group">
					{#each autofill as auto}
						<button
							{disabled}
							class="btn btn-outline-dark"
							on:click={(e) => {
								e.preventDefault();
								// @ts-ignore
								document.getElementById("message-subject").value = auto.subject;
								// @ts-ignore
								document.getElementById("message-body").value = auto.message;
							}}>{auto.name}</button
						>
					{/each}
				</div>
			</div>
		</div>
		<div class="col-12 mt-4">
			<button
				class="btn btn-success"
				{disabled}
				on:click={(e) => {
					e.preventDefault();
					if (disabled) {
						return;
					}
					// @ts-ignore
					let subject = document.getElementById("message-subject").value;
					// @ts-ignore
					let body = document.getElementById("message-body").value;
					for (const p of allPlaceholders) {
						if (body.indexOf(p) !== -1) {
							errorMessage = 'Message body contains a placeholder: "' + p + '"';
							return;
						}
					}
					disabled = true;
					request
						.post("/user/create-message", {
							userId,
							body,
							subject,
						})
						.then((d) => {
							navigate("/admin/manage-user/" + userId);
						})
						.catch((e) => {
							errorMessage = e.message;
						})
						.finally(() => {
							disabled = false;
						});
				}}>Send Message</button
			>
		</div>
	</div>
</Main>
