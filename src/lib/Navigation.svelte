<script lang="ts">
    import {Navbar, NavbarBrand, Nav, NavItem, NavLink, Collapse, NavbarToggler, Dropdown, DropdownItem, DropdownMenu, DropdownToggle} from 'sveltestrap';
    import { createEventDispatcher } from 'svelte';

	const dispatch = createEventDispatcher();

    function changeStyle(style: string) {
        dispatch('styleChange', style.toLowerCase());
    }

    let isOpen = false;

    const toggle = () => (isOpen = !isOpen);
</script>

<Navbar color="transparent" light expand="lg">
    <NavbarBrand>pk-webs</NavbarBrand>
    <NavbarToggler on:click={toggle}></NavbarToggler>
    <Collapse {isOpen} navbar expand="lg">
        <Nav class="ms-auto" navbar>
            <Dropdown nav inNavbar>
                <DropdownToggle>Styles</DropdownToggle>
                <DropdownMenu end>
                    <DropdownItem on:click={() => changeStyle("light")}>Light</DropdownItem>
                    <DropdownItem on:click={() => changeStyle("dark")}>Dark</DropdownItem>
                </DropdownMenu>
            </Dropdown>
            <NavItem>
                <NavLink href="/dash">Dash</NavLink>
            </NavItem>
            <NavItem>
                <NavLink href="/settings">Settings</NavLink>
            </NavItem>
            <NavItem>
                <NavLink href="/templates">templates</NavLink>
            </NavItem>
            <NavItem>
                <NavLink href="/public">Public</NavLink>
            </NavItem>
        </Nav>
    </Collapse>
</Navbar>