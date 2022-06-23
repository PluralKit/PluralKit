<script lang="ts">
    import { tick } from "svelte";
    import { Col, Row, Input, Label, Button, Alert, Spinner } from "sveltestrap";

    import { Member, MemberPrivacy } from '../../api/types';
    import api from '../../api';

    export let privacyOpen: boolean;
    export let member: Member;
    const togglePrivacyModal = () => (privacyOpen = !privacyOpen);
    
    let err: string;
    let loading = false;
    let success = false;

    function changeAll(e: Event) {
		const target = e.target as HTMLInputElement;
		Object.keys(privacy).forEach(x => privacy[x] = target.value);
	}

    // I can't use the hacked together Required<T> type from the bulk privacy here
    // that breaks updating the displayed privacy after submitting
    // but there's not really any way for any privacy fields here to be missing
    let privacy = member.privacy;

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
		loading = true;
		const data: Member = {privacy: privacy};
		try {
			let res = await api().members(member.id).patch({data});
            member = res;
            success = true;
		} catch (error) {
			console.log(error);
			err = error.message;
		}
		loading = false;
	}

    async function focus(el) {
        await tick();
        el.focus();
    }
</script>


    {#if err}
    <Alert color="danger">{err}</Alert>
    {/if}
    {#if success}
        <Alert color="success">Member privacy updated!</Alert>
    {/if}
    <Label><b>Set all to:</b></Label>
        <select class="form-control" on:change={(e) => changeAll(e)} aria-label="set all to" use:focus>
            <option>public</option>
            <option>private</option>
        </select>
        <hr/>
        <Row>
            {#each Object.keys(privacy) as x}
            <Col xs={12} lg={6} class="mb-3">
                <Label>{privacyNames[x]}:</Label>
                <Input type="select" bind:value={privacy[x]} aria-label={`group ${privacyNames[x]} privacy`}>
                    <option default={privacy[x] === "public"}>public</option>
                    <option default={privacy[x] === "private"}>private</option>
                </Input>
            </Col>
            {/each}
        </Row>

    {#if !loading}<Button style="flex: 0" color="primary" on:click={submit} aria-label="submit privacy edits">Submit</Button> <Button style="flex: 0" color="secondary" on:click={togglePrivacyModal} aria-label="cancel privacy edits">Back</Button>
    {:else}<Button style="flex: 0" color="primary" disabled aria-label="submit privacy edits"><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" disabled aria-label="cancel privacy edits">Back</Button>
    {/if}