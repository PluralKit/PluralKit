import React, { useState, useCallback} from 'react';
import { Router, Switch, Route, Redirect } from 'react-router-dom';
import  * as BS from 'react-bootstrap'
import Toggle from 'react-toggle'
import useDarkMode from 'use-dark-mode';

import './App.scss';
import 'bootstrap/dist/css/bootstrap.min.css';
import "react-toggle/style.css"
import { FaSun, FaMoon } from "react-icons/fa";

import Dash from './Pages/Dash.js'
import history from "./History.js";
import Footer from './Components/Footer.js'
import Public from './Pages/Public.js'
import Home from './Pages/Home.js'
import Settings from './Pages/Settings.js'

export default function App() {
  const [isLoading, setIsLoading] = useState(false);
  const [isSubmit, setIsSubmit] = useState(false);
  const [isInvalid, setIsInvalid] = useState(false);

  const [, updateState] = useState();
  const forceUpdate = useCallback(() => updateState({}), []);
  const darkMode = useDarkMode(false);

  function logOut() {
    setIsSubmit(false);
    localStorage.removeItem("token");
    localStorage.removeItem("user");
    history.push("/pk-webs");
    forceUpdate();
  }

  return (
    <div className={ `contents ${localStorage.getItem('opendyslexic') ? "opendyslexic" : ""}`}>
      <Router history={history} basename="/pk-webs">
      <BS.Navbar className="mb-5 align-items-center">
            <BS.Navbar.Brand href="/pk-webs">
                pk-webs
            </BS.Navbar.Brand>
            <BS.NavDropdown id="menu" className="mr-auto" title="Menu">
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
        <div className="content">
          <BS.Container>
            <Switch>
            <Route path="/pk-webs/dash">
              { !localStorage.getItem('token') || isInvalid ? <Redirect to="/pk-webs"/> : <Dash />
              }
              </Route>
              <Route exact path="/pk-webs">
                <Home isLoading={isLoading} setIsLoading={setIsLoading} isSubmit={isSubmit} setIsSubmit={setIsSubmit} isInvalid={isInvalid} setIsInvalid={setIsInvalid}/>
              </Route>
              <Route path="/pk-webs/profile">
                <Public />
              </Route>
              <Route path="/pk-webs/settings">
                <Settings forceUpdate={forceUpdate}/>
              </Route>
            </Switch>
          </BS.Container>
          </div>
          <Footer />
      </Router>
      </div>
  );
}
