import { useState, useCallback } from 'react';
import * as BS from 'react-bootstrap'
import useDarkMode from 'use-dark-mode';
import Toggle from 'react-toggle'
import { FaSun, FaMoon } from "react-icons/fa";

import "react-toggle/style.css"
import history from "../History.js";

export default function Navigation() {

    const [, updateState] = useState();
    const forceUpdate = useCallback(() => updateState({}), []);
        
    const darkMode = useDarkMode(false);

    function logOut() {
        localStorage.removeItem("token");
        localStorage.removeItem("user");
        history.push('/pk-webs');
        forceUpdate();
    }

    return (
        <BS.Navbar className="mb-5 align-items-center">
            <BS.Navbar.Brand href="/pk-webs">
                pk-webs
            </BS.Navbar.Brand>
            <BS.NavDropdown id="menu" className="mr-auto" title="Menu">
            { localStorage.getItem('token') ? <BS.NavDropdown.Item onClick={() => logOut()}>Log out</BS.NavDropdown.Item> : "" }
            <BS.NavDropdown.Item onClick={() => history.push('/pk-webs/dash')} >Dash</BS.NavDropdown.Item>
            <BS.NavDropdown.Item onClick={() => history.push('/pk-webs/settings')} >Settings</BS.NavDropdown.Item>
            <BS.NavDropdown.Item onClick={() => history.push('/pk-webs/profile')}>Public profile</BS.NavDropdown.Item>

            </BS.NavDropdown>
            <BS.Nav className="mr-lg-2 d-flex align-items-center row">
            <Toggle
                defaultChecked={true}
                icons={false}
                onChange={darkMode.toggle} />
                {darkMode.value ? <FaMoon className="m-1"/> : <FaSun className="m-1"/>}
            </BS.Nav>
        </BS.Navbar>
    )
}