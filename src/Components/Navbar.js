import React from 'react';

import  * as BS from 'react-bootstrap'
import Toggle from 'react-toggle'
import { FaSun, FaMoon } from "react-icons/fa";
import useDarkMode from 'use-dark-mode';

import history from "../History.js";

const Navbar = ({ setIsSubmit, forceUpdate}) => {
    const darkMode = useDarkMode(false);

    function logOut() {
        setIsSubmit(false);
        localStorage.removeItem("token");
        localStorage.removeItem("user");
        history.push("/pk-webs");
        forceUpdate();
      }

    return(
        <BS.Navbar className="mb-5 align-items-center">
            <BS.Navbar.Brand href="/pk-webs">
                pk-webs
            </BS.Navbar.Brand>
            <BS.NavDropdown id="menu" className="mr-auto" title="Menu">
            {/* for some reason just using react router's link elements doesn't work here, maybe look into that */}
            <BS.NavDropdown.Item onClick={() => history.push('/pk-webs/dash')} >Dash</BS.NavDropdown.Item>
            <BS.NavDropdown.Item onClick={() => history.push('/pk-webs/settings')} >Settings</BS.NavDropdown.Item>
            <BS.NavDropdown.Item onClick={() => history.push('/pk-webs/profile')}>Public profile</BS.NavDropdown.Item>
            { localStorage.getItem('token') ? <><hr className="my-1"/><BS.NavDropdown.Item onClick={() => logOut()}>Log out</BS.NavDropdown.Item></> : "" }

            </BS.NavDropdown>
            <BS.Nav className="mr-2 d-flex align-items-center row">
            <Toggle
                defaultChecked={true}
                icons={false}
                onChange={darkMode.toggle} />
                {darkMode.value ? <FaMoon className="m-1"/> : <FaSun className="m-1"/>}
            </BS.Nav>
        </BS.Navbar>
    );
}

export default Navbar;