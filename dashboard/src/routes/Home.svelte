<script lang="ts">
    import { onMount } from 'svelte';
    import { Container, Card, CardHeader, CardBody, CardTitle, Col, Row, Spinner, Input, Button, Label, Alert } from 'sveltestrap';
    import FaLockOpen from 'svelte-icons/fa/FaLockOpen.svelte';
    import FaLock from 'svelte-icons/fa/FaLock.svelte'
    import { loggedIn, currentUser } from '../stores';
    import { Link } from 'svelte-navigator';
    import twemoji from 'twemoji';
    import parseMarkdown from '../api/parse-markdown';

    import type { System } from '../api/types';
    import api from '../api';
    import AwaitHtml from '../components/common/AwaitHtml.svelte';

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
        try {
            if (!token) {
                throw new Error("Token cannot be empty.")
            }
            const res: System = await api().systems("@me").get({ token });
            localStorage.setItem("pk-token", token);
            localStorage.setItem("pk-user", JSON.stringify(res));
            const settings = await api().systems("@me").settings.get({ token });
            localStorage.setItem("pk-config", JSON.stringify(settings));
            err = null;
            loggedIn.update(() => true);
            currentUser.update(() => res);
        } catch (error) {
            console.log(error);
            if (error.code == 401) {
                error.message = "Invalid token";
                localStorage.removeItem("pk-token");
                localStorage.removeItem("pk-user");
                currentUser.update(() => null);
            }
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
    let htmlNamePromise: Promise<string>;
    $: if (user && user.name) {
        htmlNamePromise = parseMarkdown(user.name);
    }
    $: if (settings && settings.appearance.twemoji) {
        if (welcomeElement) twemoji.parse(welcomeElement, { base: 'https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/' });
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
                            {#if !isLoggedIn}
                            <FaLock/>
                            {:else}
                            <FaLockOpen />
                            {/if}
                        </div>Log in {#if loading} <div style="float: right"><Spinner color="primary" /></div> {/if}
                    </CardTitle>
                </CardHeader>
                <CardBody>
                    {#if loading}
                        verifying login...
                    {:else if isLoggedIn}
                        {#if user && user.name}
                            <p bind:this={welcomeElement}>Welcome, <b><AwaitHtml htmlPromise={htmlNamePromise} /></b>!</p>
                        {:else}
                            <p>Welcome!</p>
                        {/if}
                        <Link to="/dash"><Button style="float: left;" color='primary' tabindex={-1}>Go to dash</Button></Link><Button style="float: right;" color='danger' on:click={logout}>Log out</Button>
                    {:else}
                        <Row>
                            <Label>Enter your token here. You can get this by using <b>pk;token</b></Label>
                            <Col xs={12} md={10}>
                                <Input class="mb-2 mb-md-0" type="text" bind:value={token}/>
                            </Col>
                            <Col xs={12} md={2}>
                                <Button style="width: 100%" color="primary" on:click={() => login(token)}>Submit</Button>
                            </Col>
                        </Row>
                        <br>
                        <Row>
                            <Label>Or, you can</Label>
                            <Col xs={12}>
                                <Button color="dark" on:click={() => window.location.href = `https://discord.com/api/oauth2/authorize?client_id=${localStorage.isBeta ? "912009351160541225" : "466378653216014359"}&redirect_uri=${encodeURIComponent(window.location.origin + "/login/discord")}&response_type=code&scope=guilds%20identify`}>
                                    <svg width="24" height="24" xmlns="http://www.w3.org/2000/svg">
                                        <path d="M19.54 0c1.356 0 2.46 1.104 2.46 2.472v21.528l-2.58-2.28-1.452-1.344-1.536-1.428.636 2.22h-13.608c-1.356 0-2.46-1.104-2.46-2.472v-16.224c0-1.368 1.104-2.472 2.46-2.472h16.08zm-4.632 15.672c2.652-.084 3.672-1.824 3.672-1.824 0-3.864-1.728-6.996-1.728-6.996-1.728-1.296-3.372-1.26-3.372-1.26l-.168.192c2.04.624 2.988 1.524 2.988 1.524-1.248-.684-2.472-1.02-3.612-1.152-.864-.096-1.692-.072-2.424.024l-.204.024c-.42.036-1.44.192-2.724.756-.444.204-.708.348-.708.348s.996-.948 3.156-1.572l-.12-.144s-1.644-.036-3.372 1.26c0 0-1.728 3.132-1.728 6.996 0 0 1.008 1.74 3.66 1.824 0 0 .444-.54.804-.996-1.524-.456-2.1-1.416-2.1-1.416l.336.204.048.036.047.027.014.006.047.027c.3.168.6.3.876.408.492.192 1.08.384 1.764.516.9.168 1.956.228 3.108.012.564-.096 1.14-.264 1.74-.516.42-.156.888-.384 1.38-.708 0 0-.6.984-2.172 1.428.36.456.792.972.792.972zm-5.58-5.604c-.684 0-1.224.6-1.224 1.332 0 .732.552 1.332 1.224 1.332.684 0 1.224-.6 1.224-1.332.012-.732-.54-1.332-1.224-1.332zm4.38 0c-.684 0-1.224.6-1.224 1.332 0 .732.552 1.332 1.224 1.332.684 0 1.224-.6 1.224-1.332 0-.732-.54-1.332-1.224-1.332z"/>
                                    </svg>
                                    Login with Discord
                                </Button>
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

<svelte:head>
    <title>PluralKit | home</title>
</svelte:head>