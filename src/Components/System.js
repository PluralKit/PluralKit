import React, { useState, useEffect } from 'react';
import  * as BS from 'react-bootstrap'

import { FaAddressCard } from "react-icons/fa";
import defaultAvatar from '../default_discord_avatar.png'

export default function System(props) {

    const [ desc, setDesc ] = useState("");
    const user = JSON.parse(localStorage.getItem("user"));

    useEffect(() => {
    const { toHTML } = require('../Functions/discord-parser.js');

    if (user.description) {
        setDesc(toHTML(user.description));
    } else setDesc("(no description)");
}, [user.description]);

        return (
           <BS.Card className="mb-3 mt-3 w-100" >
               <BS.Card.Header className="d-flex align-items-center justify-content-between">
                  <BS.Card.Title className="float-left"><FaAddressCard className="mr-3" /> {user.name}</BS.Card.Title> 
                  { user.avatar_url ? <BS.Image src={`${user.avatar_url}`} style={{width: 50, height: 50}} className="float-right" roundedCircle /> : 
               <BS.Image src={defaultAvatar} style={{width: 50, height: 50}} className="float-right" roundedCircle />}
               </BS.Card.Header>
               <BS.Card.Body>
               <BS.Row>
                    <BS.Col className="mb-lg-3" xs={12} lg={3}><b>ID:</b> {user.id}</BS.Col>
                    <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Tag:</b> {user.tag}</BS.Col>
                    <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Timezone:</b> {user.tz}</BS.Col>
                </BS.Row>
                <p><b>Description:</b></p>
                <p dangerouslySetInnerHTML={{__html: desc}}></p>
                </BS.Card.Body>
           </BS.Card>
        )
}

   

    