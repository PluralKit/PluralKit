<script lang="ts">
    import { Container, Row, Col, Card, CardHeader, CardBody, CardTitle, FormCheck, Button, Spinner } from 'sveltestrap';
    import FaUserLock from 'svelte-icons/fa/FaUserLock.svelte';

	import api from '../api';
    import { GroupPrivacy, System } from '../api/types';
	const user: System = JSON.parse(localStorage.getItem("pk-user"));

	const capitalize = (str: string) => str[0].toUpperCase() + str.substr(1);

	let loading = false;

	// kinda hacked together from typescript's Required<T> type
	const privacy: { [P in keyof GroupPrivacy]-?: boolean; } = {
		name_privacy: false,
		description_privacy: false,
		icon_privacy: false,
		list_privacy: false,
		metadata_privacy: false,
		visibility: false,
	};
	let setPrivate = true;

	async function submit() {
		loading = true;
		const data = {};
		Object.keys(privacy).filter(x => privacy[x]).forEach(key => data[key] = setPrivate ? "private" : "public");
		await api().private.bulk_privacy.group.post({ data });
		loading = false;
	}	
</script>

<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
			<Card class="mb-4">
				<CardHeader>
					<CardTitle style="margin-top: 8px; outline: none;">
						<div class="icon d-inline-block">
							<FaUserLock />
						</div> Bulk group privacy
					</CardTitle>
				</CardHeader>
				<CardBody style="border-left: 4px solid #{user.color}">
					<b>Apply the selected privacy options</b> (<i>and leave the unselected options untouched</i>):
					<br><br>

					{#each Object.keys(privacy) as key}
						<FormCheck bind:checked={privacy[key]} label={capitalize(key.split("_")[0])}/>
					{/each}

					<br>
					<Button on:click={() => Object.keys(privacy).forEach(x => privacy[x] = true)}>Select all</Button>
					<Button on:click={() => Object.keys(privacy).forEach(x => privacy[x] = false)}>Select none</Button>
					<br><br>

					<input type="checkbox" bind:checked={setPrivate} class="form-check-input" id="privacy">
					<label for="privacy">&nbsp;Check this box to set all selected privacy settings as <b>private</b>.
						Uncheck to set to <b>public</b>.</label>
					<br><br>

					<Button color="primary" on:click={submit} bind:disabled={loading}>
						{#if loading}
						<Spinner />
						{:else}
						Submit
						{/if}
					</Button>
				</CardBody>
			</Card>
		</Col>
	</Row>
</Container>