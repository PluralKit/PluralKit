<script lang="ts">
    import { Container, Row, Col, Card, CardHeader, CardBody, CardTitle, Alert, Label, Input, Button, Spinner } from 'sveltestrap';
    import { navigate } from 'svelte-navigator';
	import FaUserLock from 'svelte-icons/fa/FaUserLock.svelte';

	import api from '../../../api';
    import type { GroupPrivacy, System } from '../../../api/types';
	const user: System = JSON.parse(localStorage.getItem("pk-user"));

	if (!user) navigate('/');

	// const capitalize = (str: string) => str[0].toUpperCase() + str.substr(1);

	let loading = false;
	let err = "";
	let success = false;

	// kinda hacked together from typescript's Required<T> type
	const privacy: { [P in keyof GroupPrivacy]-?: string; } = {
		description_privacy:  "no change",
		name_privacy: "no change",
		list_privacy: "no change",
		icon_privacy: "no change",
		visibility: "no change",
		metadata_privacy: "no change",
	};

	const privacyNames: { [P in keyof GroupPrivacy]-?: string; } = {
		name_privacy: "Name",
		description_privacy:  "Description",
		icon_privacy: "Icon",
		list_privacy: "Member list",
		metadata_privacy: "Metadata",
		visibility: "Visibility",
	};

	async function submit() {
		success = false;
		loading = true;
		const dataArray = Object.entries(privacy).filter(([, value]) => value === "no change" ? false : true);
		const data = Object.fromEntries(dataArray);
		try {
			await api().private.bulk_privacy.group.post({ data });
			success = true;
		} catch (error) {
			console.log(error);
			err = error.message;
		}
		loading = false;
	}

	function changeAll(e: Event) {
		const target = e.target as HTMLInputElement;
		Object.keys(privacy).forEach(x => privacy[x] = target.value);
	}
</script>

{#if user}
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
					{#if err}
					<Alert color="danger">{err}</Alert>
					{/if}
					{#if success}
					<Alert color="success">Group privacy updated!</Alert>
					{/if}
					<Label><b>Set all to:</b></Label>
					<Input type="select" on:change={(e) => changeAll(e)} aria-label="set all to">
						<option>no change</option>
						<option>public</option>
						<option>private</option>
					</Input>
					<hr/>
					<Row>
						{#each Object.keys(privacy) as x}
						<Col xs={12} lg={6} class="mb-3">
							<Label>{privacyNames[x]}:</Label>
							<Input type="select" bind:value={privacy[x]} aria-label={`group ${privacyNames[x]} privacy`}>
								<option default>no change</option>
								<option>public</option>
								<option>private</option>
							</Input>
						</Col>
						{/each}
					</Row>

					<Button color="primary" on:click={submit} bind:disabled={loading} aria-label="submit bulk group privacy">
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
{/if}