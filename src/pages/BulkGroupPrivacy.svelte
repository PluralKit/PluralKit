<script lang="ts">
    import { Container, Row, Col, Card, CardHeader, CardBody, CardTitle, Alert, Label, Input, Button, Spinner } from 'sveltestrap';
    import FaUserLock from 'svelte-icons/fa/FaUserLock.svelte';

	import api from '../api';
    import { GroupPrivacy, System } from '../api/types';
	const user: System = JSON.parse(localStorage.getItem("pk-user"));

	// const capitalize = (str: string) => str[0].toUpperCase() + str.substr(1);

	let loading = false;
	let err = "";
	let success = false;

	// kinda hacked together from typescript's Required<T> type
	const privacy: GroupPrivacy = {
		name_privacy: "public",
		description_privacy:  "public",
		icon_privacy: "public",
		list_privacy: "public",
		metadata_privacy: "public",
		visibility: "public",
	};

	const privacyNames: GroupPrivacy = {
		name_privacy: "Name",
		description_privacy:  "Description",
		icon_privacy: "Icon",
		list_privacy: "Member list",
		metadata_privacy: "Metadata",
		visibility: "Visbility",
	};

	let setPrivate = true;

	async function submit() {
		success = false;
		loading = true;
		const data = privacy;
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
					<Alert color="success">Member privacy updated!</Alert>
					{/if}
					<Label><b>Set all to:</b></Label>
					<Input type="select" on:change={(e) => changeAll(e)}>
						<option>public</option>
						<option>private</option>
					</Input>
					<hr/>
					<Row>
						{#each Object.keys(privacy) as x}
						<Col xs={12} lg={6} class="mb-3">
							<Label>{privacyNames[x]}:</Label>
							<Input type="select" bind:value={privacy[x]}>
								<option default={privacy[x] === "public"}>public</option>
								<option default={privacy[x] === "private"}>private</option>
							</Input>
						</Col>
						{/each}
					</Row>

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