<script lang="ts">
    import { onMount } from 'svelte';
    import { Link, useLocation, useParams, navigate } from 'svelte-navigator';
    import { Alert, Col, Container, Row, Card, CardBody, CardHeader, CardTitle, Input, Label, Button, Accordion, AccordionHeader, AccordionItem } from 'sveltestrap';
    import FaRandom from 'svelte-icons/fa/FaRandom.svelte'
    
    import CardsList from '../lib/list/CardsList.svelte';
    import api from '../api';
    import { Group, Member } from '../api/types';

    export let isPublic: boolean = false;
    export let type: string = "member";
    export let pickFromGroup: boolean = false;

    let list: Member[]|Group[] = [];
    let randomList: Member[]|Group[] = [];
    
    let loading = true;
    let err: string;

    let params = useParams();
    $: id = $params.id;
    $: groupId = $params.groupId;

    let location = useLocation();
    let searchParams = $location.search && new URLSearchParams($location.search);

    let path = $location.pathname;

    let amount: number = 1;

    if (searchParams && searchParams.get("amount")) {
        amount = parseInt(searchParams.get("amount"));
        if (amount === NaN) amount = 1;
        else if (amount > 5) amount = 5;
    }

    let usePrivateItems = false;

    if (searchParams && searchParams.get("all") === "true") usePrivateItems = true;

    let allowDoubles = false;
    if (searchParams && searchParams.get("doubles") === "true") allowDoubles = true;

    // just a hidden option to expand the cards by default regardless of your global settings
    let openByDefault = false;
    if (searchParams && searchParams.get("open") === "true") openByDefault = true;
    
    let rollCounter = 1;

    onMount(async () => {
        await fetchList(amount, usePrivateItems);
    });

    async function fetchList(amount: number, usePrivateMembers?: boolean|string) {
        err = "";
        loading = true;
        try {
            if (type === "member") { 
                if (pickFromGroup) {
                    const res: Member[] = await api().groups(groupId).members.get({auth: !isPublic});
                    list = res;
                } else {
                    const res: Member[] = await api().systems(isPublic ? id : "@me").members.get({ auth: !isPublic });
                    list = res;
                }
            }
            else if (type === "group") {
                const res: Group[] = await api().systems(isPublic ? id : "@me").groups.get({ auth: !isPublic });
                list = res;
            }
            else throw new Error(`Unknown list type ${type}`);
            randomList = randomizeList(amount, usePrivateMembers, allowDoubles);
        } catch (error) {
            console.log(error);
            err = error.message;
        }
        loading = false;
    }

    function randomizeList(amount: number, usePrivateMembers?: boolean|string, allowDoubles?: boolean|string) {
        err = "";
        let filteredList = [...list];
        if (!isPublic && (!usePrivateMembers || usePrivateMembers === "false")) filteredList = (list as Member[]).filter(item => item.privacy && item.privacy.visibility === "public" ? true : false);

        let cappedAmount = amount;
        if (amount > filteredList.length) cappedAmount = filteredList.length;

        if (cappedAmount === 0) err = `No valid ${type}s could be randomized. ${!isPublic ? `If every ${type} is privated, roll again with private ${type}s included.` : ""}`;

        let tempList = [];
        for (let i = 0; i < cappedAmount; i++) {
            let index = Math.floor(Math.random() * filteredList.length);
            tempList.push(filteredList[index]);
           
            if (!allowDoubles || allowDoubles === "false") {
                filteredList.splice(index, 1);
            }
        }
        return tempList;
    }

    function rerollList() {
        let amount = parseInt(optionAmount);
        let paramArray = [];
        if (amount > 1) paramArray.push(`amount=${amount}`);
        if (optionAllowDoubles === "true") paramArray.push("doubles=true");
        if (optionUsePrivateItems === "true") paramArray.push("all=true");
        if (openByDefault === true) paramArray.push("open=true");
        
        randomList = randomizeList(parseInt(optionAmount), optionUsePrivateItems, optionAllowDoubles);
        navigate(`${path}${paramArray.length > 0 ? `?${paramArray.join('&')}` : ""}`);
        rollCounter ++;
    }

    function capitalizeFirstLetter(string: string) {
        return string.charAt(0).toUpperCase() + string.slice(1);
    }
    
    let optionAmount = amount.toString();
    
    let optionUsePrivateItems = "false";
    if (usePrivateItems === true) optionUsePrivateItems = "true";

    let optionAllowDoubles = "false";
    if (allowDoubles === true) optionAllowDoubles = "true";

    function getItemLink(item: Member | Group): string {
        let url: string;

        if (isPublic) url = "/dash/";
        else url = "/profile/";
        
        if (type === "member") url += "m/";
        else if (type === "group") url += "g/";

        url += item.id;

        return url;
    }

    function getBackUrl() {
        let str: string;
        if (isPublic)  { 
            str = "/profile";
            if (!pickFromGroup) str += `/s/${id}`;
        } else str = "/dash"
        
        if (pickFromGroup) str += `/g/${groupId}`;

        return str;
    }
</script>

<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <Card class="mb-4">
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">
                    <div class="icon d-inline-block">
                        <FaRandom />
                    </div>Randomize {capitalizeFirstLetter(type)}s {isPublic && id ? `(${id})` : pickFromGroup ? `(${groupId})` : ""}</CardTitle>
                </CardHeader>
                <CardBody>
                    <Row>
                        <Col xs={12} lg={4} class="mb-2">
                            <Label>Amount:</Label>
                            <Input bind:value={optionAmount} type="select" aria-label="amount">
                                <option default={amount === 1}>1</option>
                                <option default={amount === 2}>2</option>
                                <option default={amount === 3}>3</option>
                                <option default={amount === 4}>4</option>
                                <option default={amount === 5}>5</option>
                            </Input>
                        </Col>
                        <Col xs={12} lg={4} class="mb-2">
                            <Label>Allow duplicates:</Label>
                            <Input bind:value={optionAllowDoubles} type="select" aria-label="allow duplicates">
                                <option value="false" default={allowDoubles === false}>no</option>
                                <option value="true" default={allowDoubles === true}>yes</option>
                            </Input>
                        </Col>
                        {#if !isPublic}
                        <Col xs={12} lg={4} class="mb-2">
                            <Label>Use all {type}s:</Label>
                            <Input bind:value={optionUsePrivateItems} type="select" aria-label="include private members">
                                <option value="false" default={usePrivateItems === false}>no (only public {type}s)</option>
                                <option value="true" default={usePrivateItems === true}>yes (include private {type}s)</option>
                            </Input>
                        </Col>
                        {/if}
                    </Row>
                    <Button color="primary" on:click={() => {rerollList()}}>
                        Reroll list
                    </Button>
                    <Link to={getBackUrl()}>
                        <Button color="secondary" tabindex={-1} aria-label={`back to ${pickFromGroup ? "group" : "system"}`}>
                            Back to {pickFromGroup ? "group" : "system"}
                        </Button>
                    </Link>
                </CardBody>
            </Card>
            {#if loading}
                <span>loading...</span>
            {:else if err}
                <Alert color="danger">{err}</Alert>
            {:else}
                <CardsList openByDefault={openByDefault} bind:list={randomList} isPublic={true} itemType={type} itemsPerPage={5} currentPage={rollCounter} fullLength={5 * rollCounter - (5 - randomList.length)} />
            {/if}
        </Col>
    </Row>
</Container>