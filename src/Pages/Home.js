import React, { useEffect } from "react";
import { useForm } from "react-hook-form";
import * as fetch from 'node-fetch';

import Loading from "../Components/Loading";
import * as BS from "react-bootstrap";
import history from "../History.js";
import { FaLock } from "react-icons/fa";

import API_URL from "../Constants/constants.js";

const Home = ({isInvalid, setIsInvalid, isLoading, setIsLoading, isSubmit, setIsSubmit, }) => {
const { register, handleSubmit } = useForm();

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
        Authorization: JSON.stringify(localStorage.getItem("token")).slice(
          1,
          -1
        ),
      },
    })
    // put all the system data in localstorage
    // TODO: remove this from localstorage? since we know how to pass stuff along components now
    // then push the user to the dash!
      .then((res) => res.json())
      .then((data) => {
        localStorage.setItem("user", JSON.stringify(data));
        setIsSubmit(true);
        setIsLoading(false);
        history.push("/pk-webs/dash");
      })
      // remove the token and user data from localstorage if there's an error, also set the token as invalid
      // TODO: an error doesn't mean the token is invalid, change this depending on what error is thrown
      .catch((error) => {
        console.log(error);
        setIsInvalid(true);
        localStorage.removeItem("token");
        localStorage.removeItem("user");
        setIsLoading(false);
      });
  }

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
         'Authorization': JSON.stringify(localStorage.getItem("token")).slice(1, -1)
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
              <FaLock className="mr-3" />
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
              <BS.Alert variant="danger">Invalid token.</BS.Alert>
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
                  type="primary"
                  onClick={() => history.push("/pk-webs/dash")}
                >
                  Continue to dash
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
    </>
  );
};

export default Home;
