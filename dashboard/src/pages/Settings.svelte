<script lang="ts">
    import { Card, CardHeader, CardBody, Container, Row, Col, CardTitle, Tooltip, Button } from 'sveltestrap';
    import Toggle from 'svelte-toggle';
    import autosize from 'svelte-autosize';
    import FaCogs from 'svelte-icons/fa/FaCogs.svelte'
    import { Config } from '../api/types';
    import api from '../api';

    let savedSettings = JSON.parse(localStorage.getItem("pk-settings"));
    let apiConfig: Config = JSON.parse(localStorage.getItem("pk-config"));

    let settings = {
        appearance: {
            banner_top: false,
            banner_bottom: true,
            gradient_background: false,
            color_background: false,
            twemoji: false
        },
        accessibility: {
            opendyslexic: false,
            pagelinks: false,
            expandedcards: false
        }
    };

    if (savedSettings) {
        settings = {...settings, ...savedSettings}
    }

    let descriptions = apiConfig?.description_templates;

    async function saveDescriptionTemplates() {
        const res = await api().systems("@me").settings.patch({ data: { description_templates: descriptions } });
        localStorage.setItem("pk-config", JSON.stringify(res));
    }

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
                    <Row class="mb-3">
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-bannertop">Show banners in the background?</span> <Toggle toggledColor="#da9317" hideLabel style="display: inline" label="Show banners in the background of pages" toggled={settings.appearance.banner_top} on:toggle={() => {settings.appearance.banner_top = !settings.appearance.banner_top; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-bannertop" placement="bottom">If enabled, shows banners from the top of the system, member and group pages.</Tooltip>
                        </Col>
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-bannerbottom">Show banners at the bottom of cards?</span> <Toggle toggledColor="#da9317" hideLabel style="display: inline" label="Remove banner from bottom of cards and pages" toggled={settings.appearance.banner_bottom} on:toggle={() => {settings.appearance.banner_bottom = !settings.appearance.banner_bottom; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-bannerbottom" placement="bottom">If enabled, shows banners at the bottom of the system, member and group cards.</Tooltip>
                        </Col>
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-twemoji">Use twemoji?</span> <Toggle toggledColor="#da9317" hideLabel style="display: inline" label="Convert all emojis to twemoji" toggled={settings.appearance.twemoji} on:toggle={() => {settings.appearance.twemoji = !settings.appearance.twemoji; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-twemoji" placement="bottom">If enabled, converts all emojis into twemoji.</Tooltip>
                        </Col>
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-colorbackground">Colored background?</span> <Toggle toggledColor="#da9317" hideLabel style="display: inline" label="Use member colors as background on pages" toggled={settings.appearance.color_background} on:toggle={() => {settings.appearance.color_background = !settings.appearance.color_background; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                                <Tooltip target="s-colorbackground" placement="bottom">If enabled, turns the background on member pages into the member's color.</Tooltip>
                        </Col>
                    </Row>
                    <h4>Accessibility</h4>
                    <hr/>
                    <Row>
                        <!-- <Col xs={12} lg={4} class="mb-2">
                            <span id="s-opendyslexic">Use the opendyslexic font?</span> <Toggle toggledColor="#da9317" hideLabel style="display: inline" label="Use the opendyslexic font" toggled={settings.accessibility.opendyslexic} on:toggle={() => {settings.accessibility.opendyslexic = !settings.accessibility.opendyslexic; localStorage.setItem("pk-settings", JSON.stringify(settings)); toggleOpenDyslexic();}}/>
                            <Tooltip target="s-opendyslexic" placement="bottom">If enabled, uses the opendyslexic font as it's main font.</Tooltip>
                        </Col> -->
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-expandedcards">Expand cards by default?</span> <Toggle toggledColor="#da9317" hideLabel style="display: inline" label="Expand cards by default in list" toggled={settings.accessibility.expandedcards} on:toggle={() => {settings.accessibility.expandedcards = !settings.accessibility.expandedcards; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-expandedcards" placement="bottom">If enabled, lists will be expanded by default (overrides page links).</Tooltip>
                        </Col>
                        <Col xs={12} lg={4} class="mb-2">
                            <span id="s-pagelinks">Use page links instead of cards?</span> <Toggle toggledColor="#da9317" hideLabel style="display: inline" label="Use page links instead of expandable cards" toggled={settings.accessibility.pagelinks} on:toggle={() => {settings.accessibility.pagelinks= !settings.accessibility.pagelinks; localStorage.setItem("pk-settings", JSON.stringify(settings));}}/>
                            <Tooltip target="s-pagelinks" placement="bottom">If enabled, the list items will not expand, but instead link to the corresponding page.</Tooltip>
                        </Col>
                    </Row>
                </CardBody>
            </Card>
        </Col>
    </Row>
    {#if apiConfig}
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <Card class="mb-4">
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                        <div class="icon d-inline-block">
                            <FaCogs />
                        </div>Templates
                    </CardTitle>
                </CardHeader>
                <CardBody>
                    <p>Templates allow you to quickly set up a member description with a specific layout. Put in the template in one of the below fields, and access it whenever you create or edit a member. You can set up to 3 templates.</p>
                    <b>Template 1</b>
                    <textarea class="form-control" bind:value={descriptions[0]} maxlength={1000} use:autosize placeholder={descriptions[0]} aria-label="Description template 1"/>
                    <br>
                    <b>Template 2</b>
                    <textarea class="form-control" bind:value={descriptions[1]} maxlength={1000} use:autosize placeholder={descriptions[1]} aria-label="Description template 2"/>
                    <br>
                    <b>Template 3</b>
                    <textarea class="form-control" bind:value={descriptions[2]} maxlength={1000} use:autosize placeholder={descriptions[2]} aria-label="Description template 3"/>
                    <br>
                    <Button on:click={saveDescriptionTemplates}>Save</Button>
                </CardBody>
            </Card>
        </Col>
    </Row>
    {/if}
</Container>

<svelte:head>
    <title>PluralKit | settings</title>
</svelte:head>

<style>
    textarea {
        resize: none;
    }
</style>
