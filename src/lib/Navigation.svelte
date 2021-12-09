<script lang="ts">
    import {Navbar, NavbarBrand, Nav, NavItem, NavLink, Collapse, NavbarToggler} from 'sveltestrap';
    let isOpen = false;

    const toggle = () => (isOpen = !isOpen);
</script>

<Navbar color="transparent" expand="lg">
    <NavbarBrand style="color: white;">pk-webs</NavbarBrand>
    <NavbarToggler on:click={toggle}></NavbarToggler>
        <Collapse {isOpen} navbar>
            <Nav class="ms-auto" navbar>
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