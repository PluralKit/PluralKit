<script lang="ts">
    import { Container, Row, Col, Card, CardHeader, CardBody, CardTitle, Label, Input, Button, Spinner, Alert } from 'sveltestrap';
    import FaUserLock from 'svelte-icons/fa/FaUserLock.svelte';

	import api from '../api';
    import { MemberPrivacy, System } from '../api/types';
import Member from './Member.svelte';
	const user: System = JSON.parse(localStorage.getItem("pk-user"));

	// const capitalize = (str: string) => str[0].toUpperCase() + str.substr(1);

	let loading = false;
	let err = "";
	let success = false;

	const privacy: MemberPrivacy = {
		description_privacy: "public",
		name_privacy: "public",
		avatar_privacy: "public",
		birthday_privacy: "public",
		pronoun_privacy: "public",
		visibility: "public",
		metadata_privacy: "public",
	};

	const privacyNames: MemberPrivacy = {
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
		const data = privacy;
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
								<option default>public</option>
								<option>private</option>
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