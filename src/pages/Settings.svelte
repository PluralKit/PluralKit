<script lang="ts">
    import { Card, CardHeader, CardBody, Container, Row, Col, CardTitle, Tooltip } from 'sveltestrap';
    import Toggle from 'svelte-toggle';
    import FaCogs from 'svelte-icons/fa/FaCogs.svelte'

    let savedSettings = JSON.parse(localStorage.getItem("pk-settings"));

    let settings = {
        appearance: {
            banner_top: true,
            banner_bottom: true,
            gradient_background: false,
            no_background: false,
            twemoji: false
        }
    };

    if (savedSettings) {
        settings = {...settings, ...savedSettings}
    };

</script>

<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <Card class="mb-4">
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                        <div class="icon d-inline-block">
                            <FaCogs />
                        </div>Personal settings
                    </CardTitle>
                </CardHeader>
                <CardBody>
                    <p>These settings are saved in your localstorage. This means that you have to reapply these every time you visit in a different browser, or clear your browser's cookies.</p>
                    <h4>Appearance</h4>
                    <hr/>
                    <Row>
                        <Col xs={12} lg={4}>
                            <span id="s-bannertop">Show banners in the background?</span> <Toggle hideLabel style="display: inline" label="Remove banner from background" toggled={settings.appearance.banner_top} on:toggle={() => {settings.appearance.banner_top = !settings.appearance.banner_top; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-bannertop" placement="bottom">Toggles banners from the top of the system, member and group pages.</Tooltip>
                        </Col>
                        <Col xs={12} lg={4}>
                            <span id="s-bannerbottom">Show banners at the bottom of cards?</span> <Toggle hideLabel style="display: inline" label="Remove banner from bottom" toggled={settings.appearance.banner_bottom} on:toggle={() => {settings.appearance.banner_bottom = !settings.appearance.banner_bottom; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-bannerbottom" placement="bottom">Toggles banners at the bottom of the system, member and group cards.</Tooltip>
                        </Col>
                        <Col xs={12} lg={4}>
                            <span id="s-twemoji">Use twemoji?</span> <Toggle hideLabel style="display: inline" label="Convert to twemoji" toggled={settings.appearance.twemoji} on:toggle={() => {settings.appearance.twemoji = !settings.appearance.twemoji; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-bannerbottom" placement="bottom">Converts all emojis to twemoji.</Tooltip>
                        </Col>
                    </Row>
                </CardBody>
            </Card>
        </Col>
    </Row>
</Container>

<svelte:head>
    <title>pk-webs | settings</title>
</svelte:head>