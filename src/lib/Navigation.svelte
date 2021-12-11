<script lang="ts">
    import {Navbar, NavbarBrand, Nav, NavItem, NavLink, Collapse, NavbarToggler, Dropdown, DropdownItem, DropdownMenu, DropdownToggle} from 'sveltestrap';
    import { loggedIn } from '../stores';
    import { Link } from 'svelte-navigator';

    export let style: string;

    let isOpen = false;
    const toggle = () => (isOpen = !isOpen);

    let loggedIn_value: boolean;

    loggedIn.subscribe(value => {
		loggedIn_value = value;
	});
</script>
<div style="background-color: #292929" class="mb-4">
    <Navbar color="light" light expand="lg">
        <NavbarBrand>pk-webs</NavbarBrand>
        <NavbarToggler on:click={toggle}></NavbarToggler>
        <Collapse {isOpen} navbar expand="lg">
            <Nav class="ms-auto" navbar>
                <Dropdown nav inNavbar>
                    <DropdownToggle color="transparent">Styles</DropdownToggle>
                    <DropdownMenu end>
                        <DropdownItem on:click={() => style = "light"}>Light</DropdownItem>
                        <DropdownItem on:click={() => style = "dark"}>Dark</DropdownItem>
                    </DropdownMenu>
                </Dropdown>
                {#if loggedIn_value || localStorage.getItem("pk-token")}
                <Dropdown nav inNavbar>
                    <DropdownToggle color="transparent">Dash</DropdownToggle>
                    <DropdownMenu end>
                        <Link style="text-decoration: none;" to="/dash" state={{tab: "system"}}><DropdownItem>System</DropdownItem></Link>
                        <Link style="text-decoration: none;" to="/dash" state={{tab: "members"}}><DropdownItem>Members</DropdownItem></Link>
                    </DropdownMenu>
                </Dropdown>
                {/if}
                <NavItem>
                    <NavLink href="/settings">Settings</NavLink>
                </NavItem>
                <NavItem>
                    <NavLink href="/templates">Templates</NavLink>
                </NavItem>
                <NavItem>
                    <NavLink href="/public">Public</NavLink>
                </NavItem>
            </Nav>
        </Collapse>
    </Navbar>
</div>