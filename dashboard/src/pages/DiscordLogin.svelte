<script lang="ts">
    import { Container, Row, Col } from 'sveltestrap';
    import { onMount } from 'svelte';

    import api from '../api';

    let text = "Loading...";
    
    onMount(async () =>
    {
        const params = new URLSearchParams(window.location.search);
        const paramkeys = [...params.keys()];
        if (paramkeys.includes("code"))
        {
            const res = await api().private.discord.callback.post({ data: { code: params.get("code"), redirect_domain: window.location.origin } });
            localStorage.setItem("pk-token", res.token);
            localStorage.setItem("pk-user", JSON.stringify(res.system));
            localStorage.setItem("pk-config", JSON.stringify(res.config));
            window.location.href = window.location.origin;
        }
        else
        {
            text = "Error: " + params.get("error_description");
        }
    });
</script>

<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
                {text}
        </Col>
    </Row>
</Container>