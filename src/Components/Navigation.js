import react from 'react';
import * as BS from 'react-bootstrap'
import useDarkMode from 'use-dark-mode';
import Toggle from 'react-toggle'
import { FaSun, FaMoon } from "react-icons/fa";

import "react-toggle/style.css"

export default function Navigation(props) {
        
    const darkMode = useDarkMode(false);

    function logOut() {
        localStorage.removeItem("token");
        localStorage.removeItem("user");
        props.setIsSubmit(false);
    }

    return (
        <BS.Navbar className="mb-3 d-flex align-items-center justify-content-between light" expand="md">
            <BS.Navbar.Brand>
                pk-web
            </BS.Navbar.Brand>
            <BS.Nav className="mr-md-2">
            <Toggle
                defaultChecked={true}
                icons={false}
                onChange={darkMode.toggle} />
                {darkMode.value ? <FaMoon className="m-1"/> : <FaSun className="m-1"/>}
            </BS.Nav>
            <BS.Form inline>
            { localStorage.getItem('token') ? <BS.Button className=" mr-md-2" variant="primary" onClick={logOut}>Log Out</BS.Button> : "" }
            </BS.Form>
        </BS.Navbar>
    )
}