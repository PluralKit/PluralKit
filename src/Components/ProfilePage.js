import React, { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import  * as BS from 'react-bootstrap'
import moment from 'moment';
import Popup from 'reactjs-popup';
import 'reactjs-popup/dist/index.css';
import autosize from 'autosize';
import Twemoji from 'react-twemoji';

import defaultAvatar from '../default_discord_avatar.png'
import { FaLink } from "react-icons/fa";

export default function ProfilePage(props) {

    const location = useLocation();
    const member = props.member;

    const [ avatar, setAvatar ] = useState('')
    const [ displayName, setDisplayName ] = useState("");
    const [ birthday, setBirthday ] = useState("");
    const [ pronouns, setPronouns ] = useState("");
    const [ color, setColor ] = useState("");
    const [ desc, setDesc ] = useState("");
    const proxyTags = member.proxy_tags;

    const [ proxyView, setProxyView ] = useState(false);

        useEffect(() => {
            autosize(document.querySelector('textarea'));
        })

    useEffect(() => {
        const { toHTML } = require('../Functions/discord-parser.js');

        if (member.display_name) {
            setDisplayName(member.display_name)
        } else setDisplayName('')

        if (member.birthday) { 
            if (member.birthday.startsWith('0004-')) {
                var bdaymoment = moment(member.birthday, 'YYYY-MM-DD').format('MMM D');
                setBirthday(bdaymoment);
            } else {
                var birthdaymoment =  moment(member.birthday, 'YYYY-MM-DD').format('MMM D, YYYY');
                setBirthday(birthdaymoment);
            }
        } else { setBirthday('');
    }

    if (member.avatar_url) {
        var avatarsmall = member.avatar_url.replace('&format=jpeg', '');
        setAvatar(avatarsmall.replace('?width=256&height=256', ''))
    } else setAvatar('')

        if (member.pronouns) {
            setPronouns(toHTML(member.pronouns))
        } else setPronouns('')

        if (member.color) {
            setColor(member.color);
        } else setColor('');

        if (member.description) {
            setDesc(toHTML(member.description));
        } else setDesc("(no description)");
    }, [member.description, member.color, member.birthday, member.display_name, member.pronouns, member.avatar_url, member.proxy_tags]);

    function copyLink() {
        var link = `https://spectralitree.github.io${location.pathname}`
        var textField = document.createElement('textarea')
        textField.innerText = link
        document.body.appendChild(textField);

        textField.select();
        textField.setSelectionRange(0, 99999);
        document.execCommand('copy');

        document.body.removeChild(textField);
    }

    return (
       <> 
       { localStorage.getItem('colorbg') ? "" : member.color ? <><div className="backdrop" style={{backgroundColor: `#${color}`}}/>
        <div className="backdrop-overlay"/></> : "" }
        <BS.Alert variant="primary" >You are currently <b>viewing</b> a member.</BS.Alert>
        <BS.Card className="mb-5">
        <BS.Card.Header className="d-flex align-items-center justify-content-between">
         <div> <BS.OverlayTrigger placement="left" overlay={ 
            <BS.Tooltip>
                Copy link
            </BS.Tooltip>
        }><BS.Button variant="link" onClick={() => copyLink()}><FaLink style={{fontSize: '1.25rem'}}/></BS.Button></BS.OverlayTrigger>
         <BS.Button variant="link" ><b>{member.name}</b> ({member.id})</BS.Button></div>
            { member.avatar_url ?   <Popup trigger={<BS.Image src={`${member.avatar_url}`} style={{width: 50, height: 50}} tabIndex="0" className="float-right" roundedCircle />} className="avatar" modal>
                {close => (
                  <div className="text-center w-100 m-0" onClick={() => close()}>
                  <div className="m-auto" style={{maxWidth: '640px'}}>
                      <BS.Image src={`${avatar}`} style={{'maxWidth': '100%', height: 'auto'}} thumbnail />
                  </div>
                </div>
                )}
            </Popup> : 
        <BS.Image src={defaultAvatar} style={{width: 50, height: 50}} tabIndex="0" className="float-right" roundedCircle />}
        </BS.Card.Header>
                <BS.Card.Body style={{ borderLeft: localStorage.getItem('colorbg') ? `5px solid #${color}` : ''}}>
            <BS.Row>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>ID:</b> {member.id}</BS.Col>
                { member.display_name ? localStorage.getItem('twemoji') ? <BS.Col className="mb-lg-3" xs={12} lg={3}><Twemoji options={{ className: 'twemoji' }}><b>Display name: </b>{displayName}</Twemoji></BS.Col> :
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Display name:</b> {displayName}</BS.Col> : "" }
                { member.birthday ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Birthday:</b> {birthday}</BS.Col> : "" }
                { member.pronouns ?  localStorage.getItem('twemoji') ? <BS.Col className="mb-lg-3" xs={12} lg={3}><Twemoji options={{ className: 'twemoji' }}><b>Pronouns:</b> <span dangerouslySetInnerHTML={{__html: pronouns}}></span></Twemoji></BS.Col> : 
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Pronouns:</b> <span dangerouslySetInnerHTML={{__html: pronouns}}></span></BS.Col> : "" }
                { member.color ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Color:</b> {color}</BS.Col> : "" }
                { proxyView ? "" : <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Proxy tags:</b> <BS.Button variant="light" size="sm" onClick={() => setProxyView(true)}>View</BS.Button></BS.Col> }
                
            </BS.Row>
            { proxyView ? <><hr/>
             <h5>Viewing proxy tags</h5>
         <BS.Row className="mb-2">
          { proxyTags.length === 0 ? <BS.Col className="mb-lg-2"><b>No proxy tags set.</b></BS.Col> : proxyTags.map((proxytag) => <BS.Col key={proxytag.index} className="mb-lg-2" xs={12} lg={2}> <code>{proxytag.prefix}text{proxytag.suffix}</code></BS.Col> )}
         </BS.Row>
         <BS.Button variant="light" onClick={() => setProxyView(false)}>Exit</BS.Button>
            <hr/></> : "" }
            <p><b>Description:</b></p>
            { localStorage.getItem('twemoji') ? <Twemoji options={{ className: 'twemoji' }}><p dangerouslySetInnerHTML={{__html: desc}}></p></Twemoji> : <p dangerouslySetInnerHTML={{__html: desc}}></p>}
            <BS.Row><BS.Col><Link to={location.pathname.substring(0, location.pathname.lastIndexOf('/'))}><BS.Button variant="primary" className="float-right">Back</BS.Button></Link></BS.Col></BS.Row>
            </BS.Card.Body>
        </BS.Card>
        </>
    )
}
