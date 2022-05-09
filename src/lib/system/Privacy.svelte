<script lang="ts">
    import { Card, CardHeader, CardBody, CardTitle, Row, Col, Button, Spinner } from 'sveltestrap';
    import FaUserLock from 'svelte-icons/fa/FaUserLock.svelte';
    import PrivacyEdit from './PrivacyEdit.svelte';

    import { System } from '../../api/types';

    export let user: System;
    let editMode = false;

    let loading: boolean;
</script>

<Card class="mb-4">
    <CardHeader>
        <CardTitle style="margin-top: 8px; outline: none;">
            <div class="icon d-inline-block">
                <FaUserLock />
            </div> System privacy
            {#if loading}<div class="d-inline-block mr-5" style="float: right;"><Spinner color="primary" /></div>{/if}
        </CardTitle>
    </CardHeader>
    <CardBody style="border-left: 4px solid #{user.color}">
        {#if editMode}
        <PrivacyEdit bind:loading bind:user={user} bind:editMode/>
        {:else}
        <Row>
            <Col xs={12} lg={4} class="mb-3">
                <b>Description:</b> {user.privacy.description_privacy}
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <b>Member list:</b> {user.privacy.member_list_privacy}
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <b>Group list:</b> {user.privacy.group_list_privacy}
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <b>Current front:</b> {user.privacy.front_privacy}
            </Col>
            <Col xs={12} lg={4} class="mb-3">
                <b>Front history:</b> {user.privacy.front_history_privacy}
            </Col>
        </Row>
        <Button style="flex: 0" color="primary" on:click={() => editMode = true}>Edit</Button>
        <Button style="flex: 0" color="secondary" on:click={() => window.location.href = window.location.origin+"/dash/bulk-member-privacy"}>Bulk member privacy</Button>
        <Button style="flex: 0" color="secondary" on:click={() => window.location.href = window.location.origin+"/dash/bulk-group-privacy"}>Bulk group privacy</Button>
        {/if}
    </CardBody>
</Card>