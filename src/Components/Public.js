import React from 'react';
import * as BS from 'react-bootstrap';
import { FaStar } from "react-icons/fa";
import { useForm } from "react-hook-form";
import history from "../History.js";
import { Switch, Route, useRouteMatch } from 'react-router-dom';
import Profile from './Profile.js'

export default function Public () {
    const { path, url } = useRouteMatch();
    const { register, handleSubmit } = useForm();

    const submitID = (data) => {
        history.push(`${url}/${data.sysID}`);
    }

    return (

        <Switch>
        <Route exact path={path}>
        <BS.Card>
            <BS.Card.Header>
            <BS.Card.Title><FaStar className="mr-3" />Profile</BS.Card.Title>
            </BS.Card.Header>
            <BS.Card.Body>
                <BS.Form onSubmit={handleSubmit(submitID)}>
                    <BS.Form.Label>
                        Submit a system ID to view to that system's profile.
                    </BS.Form.Label>
                    <BS.Form.Row>
                        <BS.Col className="mb-1"  xs={12} lg={10}>
                            <BS.Form.Control name="sysID" ref={register} defaultValue="" />
                        </BS.Col>
                        <BS.Col>
                            <BS.Button variant="primary" type="submit" block >Submit</BS.Button>
                        </BS.Col>
                    </BS.Form.Row>
                </BS.Form>
            </BS.Card.Body>
        </BS.Card>
        </Route>
        <Route path={`${path}/:sysID`}>
            <Profile />
              </Route>
        </Switch>
    )
}