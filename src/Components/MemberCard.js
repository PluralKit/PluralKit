import React, { useEffect, useState } from 'react';
import  * as BS from 'react-bootstrap'

import defaultAvatar from '../default_discord_avatar.png'
import { FaUser } from "react-icons/fa";

export default function MemberCard(props) {

    const { toHTML } = require('discord-markdown');

    const [ desc, setDesc ] = useState("");
    const member = props.member;

    useEffect(() => {
        if (member.description) {
            setDesc(toHTML(member.description));
        } else setDesc("(no description)");
    }, [member.description]);

    return (
       <> 
        <BS.Accordion.Toggle className="d-flex align-items-center justify-content-between" as={BS.Card.Header} eventKey={member.id}>
            <BS.Card.Title className="float-left"><FaUser className="mr-4" /> <b>{member.name}</b> ({member.id})</BS.Card.Title>
            { member.avatar_url ? <BS.Image src={`${member.avatar_url}`} style={{width: 50, height: 50}} className="float-right" roundedCircle /> : 
        <BS.Image src={defaultAvatar} style={{width: 50, height: 50}} className="float-right" roundedCircle />}
        </BS.Accordion.Toggle>
        <BS.Accordion.Collapse eventKey={member.id}>
            <BS.Card.Body>
            <BS.Row>
                <BS.Col xs={12} md={3}><b>ID:</b> {member.id}</BS.Col>
                { member.display_name ? <BS.Col xs={12} md={3}><b>AKA: </b>{member.display_name}</BS.Col> : "" }
                { member.birthday ? <BS.Col xs={12} md={3}><b>Birthday:</b> {member.birthday}</BS.Col> : "" }
                { member.pronouns ? <BS.Col xs={12} md={3}><b>Pronouns:</b> {member.pronouns}</BS.Col> : "" }
            </BS.Row>
            <br/>
            <p><b>Description:</b></p>
            <p dangerouslySetInnerHTML={{__html: desc}}></p>
            </BS.Card.Body>
        </BS.Accordion.Collapse>
        </>
    )
}
