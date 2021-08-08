import React from 'react';
import * as BS from 'react-bootstrap';
import { FaStar } from "react-icons/fa";
import { useForm } from "react-hook-form";
import history from "../History.js";
import { Switch, Route, useRouteMatch } from 'react-router-dom';
import Profile from '../Components/Public/Profile.js'
import MemberProfile from '../Pages/MemberProfile.js'

export default function Public () {
    const { path, url } = useRouteMatch();
    const { register: registerSys, handleSubmit: handleSys } = useForm();

    const submitSysID = (data) => {
        history.push(`${url}/${data.sysID}`);
    }

    const { register: registerMember, handleSubmit: handleMember } = useForm();

    const submitMemberID = (data) => {
        history.push(`${url}/m/${data.memberID}`);
    }

    return (

        <Switch>
        <Route exact path={path}>
        <BS.Card className="mb-3">
            <BS.Card.Header>
            <BS.Card.Title><FaStar className="mr-3" />Profile</BS.Card.Title>
            </BS.Card.Header>
            <BS.Card.Body>
                <BS.Form onSubmit={handleSys(submitSysID)}>
                    <BS.Form.Label>
                        Submit a <b>system ID</b> to view to that system's profile.
                    </BS.Form.Label>
                    <BS.Form.Row>
                        <BS.Col className="mb-1"  xs={12} lg={10}>
                            <BS.Form.Control name="sysID" {...registerSys("sysID")} defaultValue="" placeholder="Enter system ID here..."/>
                        </BS.Col>
                        <BS.Col>
                            <BS.Button variant="primary" type="submit" block >Submit</BS.Button>
                        </BS.Col>
                    </BS.Form.Row>
                </BS.Form>
            </BS.Card.Body>
        </BS.Card>
        <BS.Card>
            <BS.Card.Body>
                <BS.Form onSubmit={handleMember(submitMemberID)}>
                    <BS.Form.Label>
                        Alternatively, submit a <b>member ID</b> to view that member.
                    </BS.Form.Label>
                    <BS.Form.Row>
                        <BS.Col className="mb-1"  xs={12} lg={10}>
                            <BS.Form.Control name="memberID" {...registerMember("memberID")} defaultValue="" placeholder="Enter member ID here..." />
                        </BS.Col>
                        <BS.Col>
                            <BS.Button variant="primary" type="submit" block >Submit</BS.Button>
                        </BS.Col>
                    </BS.Form.Row>
                </BS.Form>
            </BS.Card.Body>
        </BS.Card>
        </Route>
        <Route path={`${path}/m/:memberID`}>
            <MemberProfile />
        </Route>
        <Route path={`${path}/:sysID`}>
            <Profile />
        </Route>
        </Switch>
    )
}