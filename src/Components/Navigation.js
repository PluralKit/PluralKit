import react from 'react';
import * as BS from 'react-bootstrap'


export default function Navigation(props) {

    function logOut() {
        localStorage.removeItem("token");
        localStorage.removeItem("user");
        props.setIsSubmit(false);
    }

    return (
        <BS.Navbar bg="light" className="mb-3 " expand="md">
            <BS.Navbar.Brand>
                pk-web
            </BS.Navbar.Brand>
            <BS.Form inline>
            { localStorage.getItem('token') ? <BS.Button className="float-right mr-md-2" variant="primary" onClick={logOut}>Log Out</BS.Button> : "" }
            </BS.Form>
        </BS.Navbar>
    )
}