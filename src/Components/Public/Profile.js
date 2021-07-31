import React, { useEffect, useState } from 'react';
import { useRouteMatch } from 'react-router-dom';
import { useParams } from 'react-router-dom';
import * as BS from 'react-bootstrap';
import Popup from 'reactjs-popup';
import Twemoji from 'react-twemoji';

import { FaAddressCard } from "react-icons/fa";
import defaultAvatar from '../../default_discord_avatar.png'
import Loading from "../Loading.js";
import API_URL from "../../Constants/constants.js";
import ProfileList from "./ProfileList.js";

export default function Profile () {

    const match = useRouteMatch("/profile/:sysID/:memberID");

    const { sysID } = useParams();
    const [ system, setSystem ] = useState('');
    const [ name, setName ] = useState('');
    const [ tag, setTag ] = useState("");
    const [ timezone, setTimezone ] = useState("");
    const [ desc, setDesc ] = useState("");
    const [ avatar, setAvatar ] = useState('');

    const [ isLoading, setIsLoading ] = useState(true);
    const [ isError, setIsError ] = useState(false);

    useEffect (() => {
        fetch(`${API_URL}s/${sysID}`,{
        method: 'GET'
    }).then ( res => res.json()
        ).then (data => { 
          setSystem(data);
          setIsLoading(false);
      })
        .catch (error => { 
            console.log(error);
            setIsError(true);
            setIsLoading(false);
        }) 
    }, [sysID])

    useEffect(() => {
        const { toHTML } = require('../../Functions/discord-parser.js');
        
        if (system.name) {
            setName(system.name);
        } else setName('');

        if (system.avatar_url) {
            var avatarsmall = system.avatar_url.replace('&format=jpeg', '');
            setAvatar(avatarsmall.replace('?width=256&height=256', ''))
        } else setAvatar('')

        if (system.tag) {
            setTag(system.tag);
        } else setTag('');
    
        if (system.tz) {
            setTimezone(system.tz);
        } else setTimezone('');

        if (system.description) {
            setDesc(toHTML(system.description));
        } else setDesc("(no description)");
    }, [system.description, system.tag, system.avatar_url, system.tz, system.name]);

   return (match ? <ProfileList sysID={sysID} /> :
   <>{ isLoading ? <Loading /> : isError ?  <BS.Alert variant="danger">Something went wrong, either the system doesn't exist, or there was an error fetching data.</BS.Alert> :
    <><BS.Alert variant="primary" >You are currently <b>viewing</b> a system.</BS.Alert>
        <BS.Card className="mb-3 mt-3 w-100" >
        <BS.Card.Header className="d-flex align-items-center justify-content-between">
           <BS.Card.Title className="float-left"><FaAddressCard className="mr-4 float-left" /> {name} ({system.id})</BS.Card.Title> 
           { system.avatar_url ? <Popup trigger={<BS.Image src={`${system.avatar_url}`} style={{width: 50, height: 50}} tabIndex="0" className="float-right" roundedCircle />} className="avatar" modal>
         {close => (
             <div className="text-center w-100 m-0" onClick={() => close()}>
             <BS.Image src={`${avatar}`} style={{'max-width': 640, height: 'auto'}} thumbnail />
             </div>
         )}
     </Popup> : 
        <BS.Image src={defaultAvatar} style={{width: 50, height: 50}} className="float-right" roundedCircle />}
        </BS.Card.Header>
        <BS.Card.Body>
        <BS.Row>
             <BS.Col className="mb-lg-3" xs={12} lg={3}><b>ID:</b> {system.id}</BS.Col>
             <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Tag:</b> {tag}</BS.Col>
             <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Timezone:</b> {timezone}</BS.Col>
         </BS.Row>
         <p><b>Description:</b></p>
         { localStorage.getItem('twemoji') ? <Twemoji options={{ className: 'twemoji' }}><p dangerouslySetInnerHTML={{__html: desc}}></p></Twemoji> : <p dangerouslySetInnerHTML={{__html: desc}}></p>}
         </BS.Card.Body>
    </BS.Card>
    
    <ProfileList sysID={sysID} /> </> } 
    
    </>
   )
}