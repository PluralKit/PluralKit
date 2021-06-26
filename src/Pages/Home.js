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

  const onSubmit = (data) => {
    localStorage.setItem("token", data.pkToken);
    logIn();
  };

  function logIn() {
    setIsInvalid(false);
    setIsLoading(true);

    fetch(`${API_URL}s/`, {
      method: "GET",
      headers: {
        Authorization: JSON.stringify(localStorage.getItem("token")).slice(
          1,
          -1
        ),
      },
    })
      .then((res) => res.json())
      .then((data) => {
        localStorage.setItem("user", JSON.stringify(data));
        setIsSubmit(true);
        setIsLoading(false);
        history.push("/pk-webs/dash");
      })
      .catch((error) => {
        console.log(error);
        setIsInvalid(true);
        localStorage.removeItem("token");
        localStorage.removeItem("user");
        setIsLoading(false);
      });
  }

  useEffect(() => {
    if (localStorage.getItem('token')) {
      checkLogIn();
    }
  }, []);

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
            {isSubmit && !localStorage.getItem("user") ? (
              <BS.Alert variant="danger">
                Something went wrong, please try again.
              </BS.Alert>
            ) : (
              ""
            )}
            {isInvalid ? (
              <BS.Alert variant="danger">Invalid token.</BS.Alert>
            ) : (
              ""
            )}
            {localStorage.getItem("user") && localStorage.getItem("token") ? (
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
