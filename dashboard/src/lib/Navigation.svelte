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
        <Link to="/" class="navbar-brand"><NavbarBrand tabindex={-1} class="m-0">PluralKit</NavbarBrand></Link>
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
                        <Link style="text-decoration: none;" to="/dash?tab=system"><DropdownItem tabindex={-1}>System</DropdownItem></Link>
                        <Link style="text-decoration: none;" to="/dash?tab=members"><DropdownItem tabindex={-1}>Members</DropdownItem></Link>
                        <Link style="text-decoration: none;" to="/dash?tab=groups"><DropdownItem tabindex={-1}>Groups</DropdownItem></Link>
                        <DropdownItem divider />
                        <DropdownItem on:click={logout}>Log out</DropdownItem>
                    </DropdownMenu>
                </Dropdown>
                {/if}
                <NavItem>
                    <Link to="/settings" class="nav-link">Settings</Link>
                </NavItem>
                <NavItem>
                    <Link to="/profile" class="nav-link">Public</Link>
                </NavItem>
                <NavItem>
                    <Link to="/status" class="nav-link">Bot status</Link>
                </NavItem>
            </Nav>
        </Collapse>
    </Navbar>

<style>
    .select-text {
        user-select: text;
    }
</style>