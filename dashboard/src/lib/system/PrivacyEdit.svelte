<script lang="ts">
    import { Input, Row, Col, Button, Label, Alert, Spinner } from 'sveltestrap';
    import { currentUser } from '../../stores';

    import { System, SystemPrivacy } from '../../api/types';
    import api from '../../api';

    export let user: System;
    export let editMode: boolean;

    let err: string;
    let success = false;
    let loading = false;

    function changeAll(e: Event) {
		const target = e.target as HTMLInputElement;
		Object.keys(privacy).forEach(x => privacy[x] = target.value);
	}

    let privacy = user.privacy;

    const privacyNames: { [P in keyof SystemPrivacy]-?: string; } = {
		description_privacy: "Description",
        member_list_privacy: "Member list",
        front_privacy: "Front",
        front_history_privacy: "Front history",
        group_list_privacy: "Group list",
        pronoun_privacy: "Pronouns"
	};

    async function submit() {
		loading = true;
		const data: System = {privacy: privacy};
		try {
			let res = await api().systems(user.id).patch({data});
            user = res;
            currentUser.update(() => res);
            success = true;
		} catch (error) {
			console.log(error);
			err = error.message;
		}
		loading = false;
	}
</script>


    {#if err}
    <Alert color="danger">{err}</Alert>
    {/if}
    {#if success}
        <Alert color="success">System privacy updated!</Alert>
    {/if}
    <Label><b>Set all to:</b></Label>
        <select class="form-control" on:change={(e) => changeAll(e)} aria-label="set all to">
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

{#if loading}
<Button style="flex: 0" color="primary" aria-label="submit privacy edit"><Spinner size="sm"/></Button> <Button style="flex: 0" color="secondary" aria-label="cancel privacy edit"><Spinner size="sm"/></Button>
{:else}
<Button style="flex: 0" color="primary" on:click={submit} aria-label="submit privacy edit">Submit</Button> <Button style="flex: 0" color="secondary" on:click={() => editMode = false} aria-label="cancel privacy edit">Back</Button>
{/if}