import React from 'react';

import  * as BS from 'react-bootstrap'
import Toggle from 'react-toggle'
import { FaSun, FaMoon } from "react-icons/fa";

import history from "../History.js";

const Navbar = ({ setIsSubmit, forceUpdate}) => {
    
    function toggleDarkMode() {
        if (localStorage.getItem("pk-darkmode"))
            localStorage.removeItem("pk-darkmode");
        else localStorage.setItem("pk-darkmode", "true");
        forceUpdate();
    };

    function logOut() {
        setIsSubmit(false);
        localStorage.removeItem("token");
        localStorage.removeItem("user");
        history.push("/");
        forceUpdate();
      };

    return(
        <BS.Navbar className="mb-5 align-items-center">
            <BS.Navbar.Brand href="/">
                pk-webs
            </BS.Navbar.Brand>
            <BS.NavDropdown id="menu" className="mr-auto" title="Menu">
            {/* for some reason just using react router's link elements doesn't work here, maybe look into that */}
            <BS.NavDropdown.Item onClick={() => history.push('/dash')} >Dash</BS.NavDropdown.Item>
            <BS.NavDropdown.Item onClick={() => history.push('/settings')} >Settings</BS.NavDropdown.Item>
            <BS.NavDropdown.Item onClick={() => history.push('/template')}>Templates</BS.NavDropdown.Item>
            <BS.NavDropdown.Item onClick={() => history.push('/profile')}>Public profile</BS.NavDropdown.Item>
            { localStorage.getItem('token') ? <><hr className="my-1"/><BS.NavDropdown.Item onClick={() => logOut()}>Log out</BS.NavDropdown.Item></> : "" }

            </BS.NavDropdown>
            <BS.Nav className="mr-2 d-flex align-items-center row">
            <Toggle
                defaultChecked={true}
                icons={false}
                onClick={() => toggleDarkMode()} />
                {localStorage.getItem("pk-darkmode") ? <FaMoon className="m-1"/> : <FaSun className="m-1"/>}
            </BS.Nav>
        </BS.Navbar>
    );
}

export default Navbar;