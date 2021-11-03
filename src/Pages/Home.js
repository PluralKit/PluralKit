import React, { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import * as fetch from 'node-fetch';

import Loading from "../Components/Loading";
import * as BS from "react-bootstrap";
import history from "../History.js";
import { FaLockOpen, FaHome } from "react-icons/fa";

import API_URL from "../Constants/constants.js";

const Home = ({isInvalid, setIsInvalid, isLoading, setIsLoading, isSubmit, setIsSubmit, forceUpdate }) => {
	const { register, handleSubmit } = useForm();

	const [ errorMessage, setErrorMessage ] = useState("");

	// submit login form, add the token to the localstorage
	const onSubmit = (data) => {
		localStorage.setItem("token", data.pkToken);
		logIn();
	};

	function logIn() {

		// make sure the token is not set to invalid and add a funny little spinner to indicate loading
		setIsInvalid(false);
		setIsLoading(true);
		
		// then fetch the system data with the token stored in localstorage
		fetch(`${API_URL}s/`, {
			method: "GET",
			headers: {
				Authorization: localStorage.getItem("token"),
			},
		})
		// put all the system data in localstorage
			.then((res) => {
				if (!res.ok)
					throw new Error('HTTP Status ' + res.status);
				return res.json();
			})
			.then((data) => {
				localStorage.setItem("user", JSON.stringify(data));
				setIsSubmit(true);
				setIsLoading(false);
				history.push("/dash");
			})
			// remove the token and user data from localstorage if there's an error, also set the token as invalid
			.catch((error) => {
				console.log(error);
				setIsInvalid(true);
				setErrorMessage(error.message);
				if (error.message === "HTTP Status 401")
					setErrorMessage("Your token is invalid.")
				localStorage.removeItem("token");
				localStorage.removeItem("user");
				setIsLoading(false);
			});
	}

	// Logout function
	function logOut() {
		setIsSubmit(false);
		localStorage.removeItem("token");
		localStorage.removeItem("user");
		history.push("/");
		forceUpdate();
	};

	// when the homepage loads, check if there's a token, if there is, check if it's still valid
	// removing the dependency array causes a rerender loop, so just ignore ESlint here
	useEffect(() => {
		if (localStorage.getItem('token')) {
			checkLogIn();
		}
	}, []);

	// very similar to LogIn(), only difference is that it doesn't push the user afterwards
	// TODO: useless double code that probably could be refactored somehow
	function checkLogIn() {
		setIsInvalid(false);
		setIsLoading(true);
		
		 fetch(`${API_URL}s/`,{
			 method: 'GET',
			 headers: {
				 'Authorization': localStorage.getItem("token")
			 }}).then ( res => res.json()
			 ).then (data => { 
				 localStorage.setItem('user', JSON.stringify(data));
				 setIsSubmit(true);
				 setIsLoading(false);
		 })
			 .catch (error => { 
				 console.log(error);
				 setIsInvalid(true);
				 localStorage.removeItem('token');
				 localStorage.removeItem('user');
				 setIsLoading(false);
			 })
		 };

	return (
		<>
		{/* if the page is loading, just show the loading component */}
			{isLoading ? (
				<Loading />
			) : (
				<BS.Card className="mb-3 mt-3">
					<BS.Card.Header className="d-flex align-items-center justify-content-between">
						<BS.Card.Title>
							<FaLockOpen className="mr-3" />
							Login
						</BS.Card.Title>
					</BS.Card.Header>
					<BS.Card.Body>
						{/* if the login form has been submitted, and there's no user object, show a generic error */}
						{isSubmit && !localStorage.getItem("user") ? (
							<BS.Alert variant="danger">
								Something went wrong, please try again.
							</BS.Alert>
						) : (
							""
						)}
						{/* if the inserted token is invalid, show invalid error! 
							this also shows if the token used in checkLogIn() is invalid */}
						{isInvalid ? (
							<BS.Alert variant="danger">{errorMessage}</BS.Alert>
						) : (
							""
						)}
						{ // if there's a user object in localstorage, and there's a token in localstorage, the user is logged in already
						localStorage.getItem("user") && localStorage.getItem("token") ? (
							<>
								<p>
									You are logged in already, click here to continue to the dash.
								</p>
								<BS.Button
									variant="primary"
									onClick={() => history.push("/dash")}
								>
									Continue to dash
								</BS.Button>
								<BS.Button style={{float: 'right'}} variant="danger"
									onClick={() => logOut()}
								>Log out
								</BS.Button>
							</>
						) : (
							// otherwise, show login form
							<BS.Form onSubmit={handleSubmit(onSubmit)}>
								<BS.Form.Row>
									<BS.Col className="mb-1" xs={12} lg={10}>
										<BS.Form.Label>
											Enter your token here. You can get your token by using{" "}
											<b>"pk;token"</b>.
										</BS.Form.Label>
									</BS.Col>
								</BS.Form.Row>
								<BS.Form.Row>
									<BS.Col xs={12} lg={10}>
										<BS.Form.Control
											required
											name="pkToken"
											type="text"
											{...register("pkToken")}
											placeholder="token"
										/>
									</BS.Col>
									<BS.Col>
										<BS.Button variant="primary" type="submit" block>
											Submit
										</BS.Button>
									</BS.Col>
								</BS.Form.Row>
							</BS.Form>
						)}
					</BS.Card.Body>
				</BS.Card>
			)}
			<BS.Card>
			<BS.Card.Header>
				<BS.Card.Title>
					<FaHome className="mr-3" />
					Welcome!
				</BS.Card.Title>
			</BS.Card.Header>
			<BS.Card.Body>
				<p>Pk-webs is a web interface for PluralKit, it lets you edit your system and members using PluralKit's API, without having to use commands on discord.</p>
				<blockquote>
					<p>This website is an ongoing project and will be sporadically updated. If you have any issues or questions, join <a href='https://discord.gg/PczBt78'>PluralKit's support server</a> and ping us (Fulmine#1917). Since this project is unofficial, the actual pluralkit devs will not be able to help you with everything.</p>
				</blockquote>
				<hr/>
				<h5>FAQ:</h5>
				Will groups be added to this website?
				<blockquote>In the future, yes! Groups are not in the API as of now, which is what this site depends on. When APIv2 comes out, they will eventually be added here too.</blockquote>
				Will switch history/editing switches be added to this website?
				<blockquote>Probably when APIv2 comes out, right now the API is very limited in how it can handle switches. And we'd rather add everything in one go.</blockquote>
				What about bulk editing?
				<blockquote>Probably not. Bulk commands are planned for the main bot, and we're not sure how to fit them into the main site at the moment.</blockquote>
				Can you add [other feature]?
				<blockquote>Depends on the feature. Ping us in the support server and we'll let you know if it's feasible or not. If it's accessibility related, chances are good that we'll add it.</blockquote>
				Can i contribute to this?
				<blockquote>Yeah sure! The code is open source on <a href='https://github.com/Spectralitree/pk-webs/'>github</a>. Be warned though that we're currently in the middle of cleaning it all up and adding comments to everything, so some sections are quite messy still.</blockquote>
			</BS.Card.Body>
			</BS.Card>
		</>
	);
};

export default Home;
