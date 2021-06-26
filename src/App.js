import React, { useState, useCallback} from 'react';
import { Router, Switch, Route, Redirect } from 'react-router-dom';
import  * as BS from 'react-bootstrap'

import './App.scss';
import 'bootstrap/dist/css/bootstrap.min.css';
import "react-toggle/style.css"

import Dash from './Pages/Dash.js'
import history from "./History.js"
import Footer from './Components/Footer.js'
import Public from './Pages/Public.js'
import Home from './Pages/Home.js'
import Settings from './Pages/Settings.js'
import Navbar from './Components/Navbar.js'

export default function App() {
  const [isLoading, setIsLoading] = useState(false);
  const [isSubmit, setIsSubmit] = useState(false);
  const [isInvalid, setIsInvalid] = useState(false);

  const [, updateState] = useState();
  const forceUpdate = useCallback(() => updateState({}), []);

  return (
    <div className={ `contents ${localStorage.getItem('opendyslexic') ? "opendyslexic" : ""}`}>
      <Router history={history} basename="/pk-webs">
        <Navbar forceUpdate={forceUpdate} setIsSumbit={setIsSubmit} />
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
