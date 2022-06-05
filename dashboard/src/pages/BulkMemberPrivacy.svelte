<script lang="ts">
    import { Container, Row, Col, Card, CardHeader, CardBody, CardTitle, Label, Input, Button, Spinner, Alert } from 'sveltestrap';
    import { navigate } from 'svelte-navigator';
	import FaUserLock from 'svelte-icons/fa/FaUserLock.svelte';

	import api from '../api';
    import { MemberPrivacy, System } from '../api/types';
	const user: System = JSON.parse(localStorage.getItem("pk-user"));

	if (!user) navigate('/');

	// const capitalize = (str: string) => str[0].toUpperCase() + str.substr(1);

	let loading = false;
	let err = "";
	let success = false;

	const privacy: { [P in keyof MemberPrivacy]-?: string; } = {
		description_privacy: "no change",
		name_privacy: "no change",
		avatar_privacy: "no change",
		birthday_privacy: "no change",
		pronoun_privacy: "no change",
		visibility: "no change",
		metadata_privacy: "no change",
	};

	const privacyNames: { [P in keyof MemberPrivacy]-?: string; } = {
		avatar_privacy: "Avatar",
		birthday_privacy: "Birthday",
		description_privacy: "Description",
		metadata_privacy: "Metadata",
		name_privacy: "Name",
		pronoun_privacy: "Pronouns",
		visibility: "Visibility",
	};

	async function submit() {
		success = false;
		loading = true;
		const dataArray = Object.entries(privacy).filter(([, value]) => value === "no change" ? false : true);
		const data = Object.fromEntries(dataArray);
		try {
			await api().private.bulk_privacy.member.post({ data });
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
						</div> Bulk member privacy
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
							<Input type="select" bind:value={privacy[x]} aria-label={`member ${privacyNames[x]} privacy`}>
								<option default>no change</option>
								<option>public</option>
								<option>private</option>
							</Input>
						</Col>
						{/each}
					</Row>

					<Button color="primary" on:click={submit} bind:disabled={loading} aria-label="submit bulk member privacy">
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