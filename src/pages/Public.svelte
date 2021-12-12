<script lang="ts">
    import { Container, Row, Col, Card, CardHeader, CardTitle, CardBody, Input, Button } from 'sveltestrap';
    import { Link } from 'svelte-navigator';
    import FaRocket from 'svelte-icons/fa/FaRocket.svelte';

    let sysInput: string = "";
</script>

<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={10}>
            <Card>
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                        <div class="icon d-inline-block">
                            <FaRocket />
                        </div>Profile
                    </CardTitle>
                </CardHeader>
                <CardBody>
                    Submit a <b>system ID</b> to view that system's profile.
                    <Row>
                        <Col xs={12} lg={9} class="my-2">
                            <Input bind:value={sysInput} />
                        </Col>
                        <Col xs={12} lg={3} class="my-2 d-flex">
                            {#if sysInput !== ""}
                            <Link style="text-decoration: none; flex: 1 0 auto" to="/public/s/{sysInput.trim()}"><Button class="w-100" color="primary">View</Button></Link>
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