<script lang="ts">
    import { Card, CardHeader, CardBody, CardTitle, Row, Col, Button } from 'sveltestrap';
    import FaUserLock from 'svelte-icons/fa/FaUserLock.svelte';
    import type Sys from '../../api/system';

    export let user: Sys;
    let editMode = false;
</script>

<Card class="mb-4">
    <CardHeader>
        <CardTitle style="margin-top: 8px; outline: none;">
            <div class="icon d-inline-block">
                <FaUserLock />
            </div> System privacy
        </CardTitle>
    </CardHeader>
    <CardBody>
        {#if editMode}
        stuff
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
        {/if}
    </CardBody>
</Card>