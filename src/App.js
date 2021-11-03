import React, { useState, useCallback, useEffect } from 'react';
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
import Template from './Pages/Template.js'
import Navbar from './Components/Navbar.js'

export default function App() {

	const [isLoading, setIsLoading] = useState(false);
	const [isSubmit, setIsSubmit] = useState(false);
	const [isInvalid, setIsInvalid] = useState(false);

	const [, updateState] = useState();
	const forceUpdate = useCallback(() => updateState({}), []);

	useEffect(() => {
		if (localStorage.getItem("pk-darkmode")) {
			document.body.classList.add('dark-mode')
		}
		else {
				document.body.classList.remove('dark-mode')
		}
		forceUpdate();
}, []);

	return (
		<div className={ `contents ${localStorage.getItem('opendyslexic') ? "opendyslexic" : ""}`}>
			<Router history={history}>
				<Navbar forceUpdate={forceUpdate} setIsSubmit={setIsSubmit} />
				<div className="content">
					<BS.Container>
						<Switch>
						<Route path="/dash">
							{ !localStorage.getItem('token') || isInvalid ? <Redirect to="/"/> : <Dash />
							}
							</Route>
							<Route exact path="/">
								<Home forceUpdate={forceUpdate} isLoading={isLoading} setIsLoading={setIsLoading} isSubmit={isSubmit} setIsSubmit={setIsSubmit} isInvalid={isInvalid} setIsInvalid={setIsInvalid}/>
							</Route>
							<Route path="/profile">
								<Public />
							</Route>
							<Route path="/settings">
								<Settings forceUpdate={forceUpdate}/>
							</Route>
							<Route path="/template">
								<Template/>
							</Route>
						</Switch>
					</BS.Container>
					</div>
					<Footer />
			</Router>
			</div>
	);
}
