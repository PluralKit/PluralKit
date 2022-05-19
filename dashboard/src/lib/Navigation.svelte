<script lang="ts">
    import {Navbar, NavbarBrand, Nav, NavItem, NavLink, Collapse, NavbarToggler, Dropdown, DropdownItem, DropdownMenu, DropdownToggle, Button} from 'sveltestrap';
    import { loggedIn } from '../stores';
    import { Link, navigate } from 'svelte-navigator';
import { get } from 'svelte/store';

    export let style: string;

    let isOpen = false;
    const toggle = () => (isOpen = !isOpen);

    let loggedIn_value: boolean;

    loggedIn.subscribe(value => {
		loggedIn_value = value;
	});
    
    function logout() {
        localStorage.removeItem("pk-token");
        localStorage.removeItem("pk-user");
        loggedIn.update(() => false);
        navigate("/");
    }

</script>
    <Navbar color="light" light expand="lg" class="mb-4">
        <NavbarBrand>PluralKit</NavbarBrand>
        <NavbarToggler on:click={toggle}></NavbarToggler>
        <Collapse {isOpen} navbar expand="lg">
            <Nav class="ms-auto" navbar>
                <Dropdown nav inNavbar>
                    <DropdownToggle color="transparent" class="nav-link"><span class="select-text">Styles</span></DropdownToggle>
                    <DropdownMenu end>
                        <DropdownItem on:click={() => style = "light"}>Light</DropdownItem>
                        <DropdownItem on:click={() => style = "dark"}>Dark</DropdownItem>
                    </DropdownMenu>
                </Dropdown>
                {#if loggedIn_value || localStorage.getItem("pk-token")}
                <Dropdown nav inNavbar>
                    <DropdownToggle color="transparent" class="nav-link"><span class="select-text">Dash</span></DropdownToggle>
                    <DropdownMenu end>
                        <Link style="text-decoration: none;" to="/dash?tab=system"><DropdownItem>System</DropdownItem></Link>
                        <Link style="text-decoration: none;" to="/dash?tab=members"><DropdownItem>Members</DropdownItem></Link>
                        <Link style="text-decoration: none;" to="/dash?tab=groups"><DropdownItem>Groups</DropdownItem></Link>
                        <DropdownItem divider />
                        <DropdownItem on:click={logout}>Log out</DropdownItem>
                    </DropdownMenu>
                </Dropdown>
                {/if}
                <NavItem>
                    <NavLink href="/settings">Settings</NavLink>
                </NavItem>
                <NavItem>
                    <NavLink href="/profile">Public</NavLink>
                </NavItem>
            </Nav>
        </Collapse>
    </Navbar>

<style>
    .select-text {
        user-select: text;
    }
</style>