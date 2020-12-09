import React, {useEffect, useState} from 'react';
import { Router, Switch, Route, Redirect } from 'react-router-dom';
import  * as BS from 'react-bootstrap'
import { useForm } from "react-hook-form";
import * as fetch from 'node-fetch';

import './App.scss';
import 'bootstrap/dist/css/bootstrap.min.css';
import { FaLock } from "react-icons/fa";

import Dash from './Components/Dash.js'
import history from "./History.js";
import Loading from "./Components/Loading.js";
import Navigation from "./Components/Navigation.js";

import API_URL from "./Constants/constants.js";

export default function App() {

  const [isLoading, setIsLoading ] = useState(false);
  const [isSubmit, setIsSubmit ] = useState(false);
  const [isInvalid, setIsInvalid] = useState(false);

  const { register, handleSubmit } = useForm();

  useEffect(() => {
    if (localStorage.getItem('token')) {
      logIn();
    }
  }, [])

  const onSubmit = data => {
    localStorage.setItem('token', data.pkToken);
    logIn();
  };

 function logIn() {
     setIsInvalid(false);
     setIsLoading(true);
     
      fetch(`${API_URL}s/`,{
        method: 'get',
        headers: {
          'Authorization': JSON.stringify(localStorage.getItem("token")).slice(1, -1)
        }}).then ( res => res.json()
        ).then (data => { 
          localStorage.setItem('user', JSON.stringify(data));
          setIsSubmit(true);
          setIsLoading(false);
          history.push("/pk-webs/dash");
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
      <Router history={history}>
        <Navigation isSubmit={isSubmit} setIsSubmit={setIsSubmit} />
          <BS.Container>
            <Switch>
            <Route path="/pk-webs/dash" >
              { !localStorage.getItem('token') || isInvalid ? <Redirect to="/pk-webs"/> : <Dash/>
              }
              </Route>
              <Route exact path="/pk-webs">
                { isLoading ? <Loading /> :
            <BS.Card className="mb-3 mt-3">
            <BS.Card.Header className="d-flex align-items-center justify-content-between">
              <BS.Card.Title><FaLock className="mr-3" />Login</BS.Card.Title>
            </BS.Card.Header>
            <BS.Card.Body>
            <BS.Form onSubmit={handleSubmit(onSubmit)}>
              { isSubmit && !localStorage.getItem('user') ? <BS.Alert variant="danger">Something went wrong, please try again.</BS.Alert> : ""}
              { isInvalid ? <BS.Alert variant="danger">Invalid token.</BS.Alert> : "" }
            <BS.Form.Row>
                <BS.Col xs={12} lg={10}>
                    <BS.Form.Label>Enter your token here. You can get your token by using <b>"pk;token"</b>.</BS.Form.Label>
                </BS.Col>
            </BS.Form.Row>
            <BS.Form.Row>
              <BS.Col xs={12} lg={10}>
                <BS.Form.Control required name="pkToken" type="text" ref={register} placeholder="token" />
              </BS.Col>
              <BS.Col>
                <BS.Button variant="primary" type="submit" block >Submit</BS.Button>
              </BS.Col>
            </BS.Form.Row>
          </BS.Form>
          </BS.Card.Body>
          </BS.Card>
          }
              </Route>
            </Switch>
          </BS.Container>
      </Router>
  );
}
