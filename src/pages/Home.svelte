<script lang="ts">
    import { onMount } from 'svelte';
    import { Container, Card, CardHeader, CardBody, CardTitle, Col, Row, Spinner, Input, Button, Label, Alert } from 'sveltestrap';
    import FaLockOpen from 'svelte-icons/fa/FaLockOpen.svelte';
    import { loggedIn, currentUser } from '../stores';
    import { Link } from 'svelte-navigator';
    import twemoji from 'twemoji';
    import { toHTML } from 'discord-markdown';

    import PKAPI from '../api/index';
    import type Sys from '../api/system';

    let loading = false;
    let err: string;
    let token: string;

    let isLoggedIn: boolean;
    let user;

    loggedIn.subscribe(value => {
		isLoggedIn = value;
	});

    currentUser.subscribe(value => {
        user = value;
    })

    onMount(() => { 
        if (localStorage.getItem("pk-token")) {
            login(localStorage.getItem("pk-token"));
        }
    });

    async function login(token: string) {
        loading = true;
        const api = new PKAPI();
        try {
            if (!token) {
                throw new Error("Token cannot be empty.")
            }
            const res: Sys = await api.getSystem({token: token});
            localStorage.setItem("pk-token", token);
            localStorage.setItem("pk-user", JSON.stringify(res));
            err = null;
            loggedIn.update(() => true);
            currentUser.update(() => res);
        } catch (error) {
            console.log(error);
            localStorage.removeItem("pk-token");
            localStorage.removeItem("pk-user");
            currentUser.update(() => null);
            err = error.message;
        }
        loading = false;
    }


    function logout() {
        token = null;
        localStorage.removeItem("pk-token");
        localStorage.removeItem("pk-user");
        loggedIn.update(() => false);
        currentUser.update(() => null);
    }

    let settings = JSON.parse(localStorage.getItem("pk-settings"));
    let welcomeElement: any;
    let htmlName: string;
    $: if (user && user.name) {
        htmlName = toHTML(user.name);
    }
    $: if (settings && settings.appearance.twemoji) {
        if (welcomeElement) twemoji.parse(welcomeElement);
    }

</script>

<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            {#if err}
                <Alert color="danger" >{err}</Alert>
            {/if}
            <Card class="mb-4">
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                        <div class="icon d-inline-block">
                            <FaLockOpen />
                        </div>Log in {#if loading} <div style="float: right"><Spinner color="primary" /></div> {/if}
                    </CardTitle>
                </CardHeader>
                <CardBody>
                    {#if loading}
                        verifying login...
                    {:else if isLoggedIn}
                        {#if user && user.name}
                            <p bind:this={welcomeElement}>Welcome, <b>{@html htmlName}</b>!</p>
                        {:else}
                            <p>Welcome!</p>
                        {/if}
                        <Link to="/dash"><Button style="float: left;" color='primary'>Go to dash</Button></Link><Button style="float: right;" color='danger' on:click={logout}>Log out</Button>
                    {:else}
                        <Row>
                            <Label>Enter your token here. You can get this by using <b>pk;token</b></Label>
                            <Col xs={12} md={10}>
                                <Input class="mb-2" type="text" bind:value={token}/>
                            </Col>
                            <Col xs={12} md={2}>
                                <Button style="width: 100%" color="primary" on:click={() => login(token)}>Submit</Button>
                            </Col>
                        </Row>
                    {/if}
                </CardBody>
            </Card>
            {#if isLoggedIn}
            <Card class="mb-4">
                <CardBody>
                    Some cool stuff will go here.
                </CardBody>
            </Card>
            {/if}
        </Col>
    </Row>
</Container>