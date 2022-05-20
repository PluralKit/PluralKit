<script lang="ts">
    import { Container, Row, Col, Card, CardHeader, CardTitle, CardBody, Input, Button } from 'sveltestrap';
    import { Link, navigate } from 'svelte-navigator';
    import FaRocket from 'svelte-icons/fa/FaRocket.svelte';
    import FaStar from 'svelte-icons/fa/FaStar.svelte'
    import FaMoon from 'svelte-icons/fa/FaMoon.svelte'

    let sysInput: string = "";
    let memberInput: string = "";
    let groupInput: string = "";
</script>

<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={12}>
            <Card class="mb-4">
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                        <div class="icon d-inline-block">
                            <FaRocket />
                        </div>System Profile
                    </CardTitle>
                </CardHeader>
                <CardBody>
                    Submit a <b>system ID</b> to view that system's profile.
                    <Row>
                        <Col xs={12} lg={9} class="my-2">
                            <Input on:keyup={(event) => {if (event.key === "Enter" && sysInput !== "") navigate(`/profile/s/${sysInput.toLowerCase().trim()}`)}} bind:value={sysInput} aria-label="enter system id to view system profile"/>
                        </Col>
                        <Col xs={12} lg={3} class="my-2 d-flex">
                            {#if sysInput !== ""}
                            <Link style="text-decoration: none; flex: 1 0 auto" to="/profile/s/{sysInput.toLowerCase().trim()}"><Button class="w-100" color="primary">View</Button></Link>
                            {:else}
                                <Button class="w-100" disabled color="primary">View</Button>
                            {/if}
                        </Col>
                    </Row>
                </CardBody>
            </Card>

            <Card class="mb-4">
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                        <div class="icon d-inline-block">
                            <FaStar />
                        </div>Member Card
                    </CardTitle>
                </CardHeader>
                <CardBody>
                    Submit a <b>member ID</b> to view that member's profile.
                    <Row>
                        <Col xs={12} lg={9} class="my-2">
                            <Input on:keyup={(event) => {if (event.key === "Enter" && memberInput !== "") navigate(`/profile/m/${memberInput.toLowerCase().trim()}`)}} bind:value={memberInput} aria-label="enter member id to view member profile"/>
                        </Col>
                        <Col xs={12} lg={3} class="my-2 d-flex">
                            {#if memberInput !== ""}
                            <Link style="text-decoration: none; flex: 1 0 auto" to="/profile/m/{memberInput.toLowerCase().trim()}"><Button class="w-100" color="primary">View</Button></Link>
                            {:else}
                                <Button class="w-100" disabled color="primary">View</Button>
                            {/if}
                        </Col>
                    </Row>
                </CardBody>
            </Card>

            <Card class="mb-4">
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                        <div class="icon d-inline-block">
                            <FaMoon />
                        </div>Group Card
                    </CardTitle>
                </CardHeader>
                <CardBody>
                    Submit a <b>group ID</b> to view that group's profile.
                    <Row>
                        <Col xs={12} lg={9} class="my-2">
                            <Input on:keyup={(event) => {if (event.key === "Enter" && groupInput !== "") navigate(`/profile/g/${groupInput.toLowerCase().trim()}`)}} bind:value={groupInput} aria-label="enter group id to view group profile"/>
                        </Col>
                        <Col xs={12} lg={3} class="my-2 d-flex">
                            {#if groupInput !== ""}
                            <Link style="text-decoration: none; flex: 1 0 auto" to="/profile/g/{groupInput.toLowerCase().trim()}"><Button class="w-100" color="primary">View</Button></Link>
                            {:else}
                                <Button class="w-100" disabled color="primary">View</Button>
                            {/if}
                        </Col>
                    </Row>
                </CardBody>
            </Card>
        </Col>
    </Row>
</Container>

<svelte:head>
    <title>PluralKit | profile</title>
</svelte:head>