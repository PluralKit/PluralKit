<script lang="ts">
    import { Card, CardHeader, CardBody, Container, Row, Col, CardTitle, Tooltip } from 'sveltestrap';
    import Toggle from 'svelte-toggle';
    import FaCogs from 'svelte-icons/fa/FaCogs.svelte'

    let savedSettings = JSON.parse(localStorage.getItem("pk-settings"));

    let settings = {
        appearance: {
            banner_top: false,
            banner_bottom: true,
            gradient_background: false,
            color_background: false,
            twemoji: false
        },
        accessibility: {
            opendyslexic: false
        }
    };

    if (savedSettings) {
        settings = {...settings, ...savedSettings}
    };

    function toggleOpenDyslexic() {
        if (settings.accessibility.opendyslexic) document.getElementById("app").classList.add("dyslexic");
        else document.getElementById("app").classList.remove("dyslexic");
    }

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
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-bannertop">Show banners in the background?</span> <Toggle hideLabel style="display: inline" label="Remove banner from background" toggled={settings.appearance.banner_top} on:toggle={() => {settings.appearance.banner_top = !settings.appearance.banner_top; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-bannertop" placement="bottom">If enabled, shows banners from the top of the system, member and group pages.</Tooltip>
                        </Col>
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-bannerbottom">Show banners at the bottom of cards?</span> <Toggle hideLabel style="display: inline" label="Remove banner from bottom" toggled={settings.appearance.banner_bottom} on:toggle={() => {settings.appearance.banner_bottom = !settings.appearance.banner_bottom; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-bannerbottom" placement="bottom">If enabled, shows banners at the bottom of the system, member and group cards.</Tooltip>
                        </Col>
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-twemoji">Use twemoji?</span> <Toggle hideLabel style="display: inline" label="Convert to twemoji" toggled={settings.appearance.twemoji} on:toggle={() => {settings.appearance.twemoji = !settings.appearance.twemoji; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-bannerbottom" placement="bottom">If enabled, converts all emojis into twemoji.</Tooltip>
                        </Col>
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-colorbackground">Colored background?</span> <Toggle hideLabel style="display: inline" label="Member color as background" toggled={settings.appearance.color_background} on:toggle={() => {settings.appearance.color_background = !settings.appearance.color_background; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                                <Tooltip target="s-colorbackground" placement="bottom">If enabled, turns the background on member pages into the member's color.</Tooltip>
                        </Col>
                    </Row>
                    <h4>Accessibility</h4>
                    <hr/>
                    <Row>
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-opendyslexic">Use the opendyslexic font?</span> <Toggle hideLabel style="display: inline" label="Use the opendyslexic font" toggled={settings.accessibility.opendyslexic} on:toggle={() => {settings.accessibility.opendyslexic = !settings.accessibility.opendyslexic; localStorage.setItem("pk-settings", JSON.stringify(settings)); toggleOpenDyslexic();}}/>
                            <Tooltip target="s-opendyslexic" placement="bottom">If enabled, uses the opendyslexic font as it's main font.</Tooltip>
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