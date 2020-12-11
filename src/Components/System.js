import React, { useState, useEffect } from 'react';
import  * as BS from 'react-bootstrap'
import { useForm, Controller } from "react-hook-form";
import autosize from 'autosize';
import moment from 'moment';
import 'moment-timezone';

import API_URL from "../Constants/constants.js";

import defaultAvatar from '../default_discord_avatar.png'
import { FaAddressCard } from "react-icons/fa";

export default function System(props) {

    const { register, handleSubmit,control } = useForm();

    const [ user, setUser ] = useState(JSON.parse(localStorage.getItem('user')));

    const [ tag, setTag ] = useState("");
    const [ timezone, setTimezone ] = useState("");
    const [ avatar, setAvatar ] = useState("");
    const [ desc, setDesc ] = useState("");
    const [ editDesc, setEditDesc ] = useState("");

    const [ editMode, setEditMode ] = useState(false);
    const [ privacyMode, setPrivacyMode ] = useState(false);
    const [ privacyView, setPrivacyView ] = useState(false);

    const [ invalidTimezone, setInvalidTimezone ] = useState(false);
    const [ errorAlert, setErrorAlert ] = useState(false);


    useEffect(() => {
    const { toHTML } = require('../Functions/discord-parser.js');
    
    if (user.tag) {
        setTag(user.tag);
    } else setTag('');

    if (user.tz) {
        setTimezone(user.tz);
    } else setTimezone('');

    if (user.avatar_url) {
        setAvatar(user.avatar_url)
    } else setAvatar('')

    if (user.description) {
        setDesc(toHTML(user.description));
        setEditDesc(user.description);
    } else { setDesc("(no description)");
    setEditDesc("");
}}, [user.description, user.tag, user.avatar_url, user.tz]);

useEffect(() => {
    autosize(document.querySelector('textarea'));
})

const submitEdit = data => {
    if (data.tz) {
        if (!moment.tz.zone(data.tz)) {
        setInvalidTimezone(true);
        return;
        }
    }

    fetch(`${API_URL}s`,{
        method: 'PATCH',
        body: JSON.stringify(data),
        headers: {
            'Content-Type': 'application/json',
            'Authorization': JSON.stringify(localStorage.getItem("token")).slice(1, -1)
        }}).then (res => res.json()
        ).then ( data => { setUser(prevState => {return {...prevState, ...data}}); localStorage.setItem('user', JSON.stringify(user)); setEditMode(false)}
        ).catch (error => {
            console.error(error);
            setErrorAlert(true);
        })

    }

const submitPrivacy = data => {
    fetch(`${API_URL}s`,{
        method: 'PATCH',
        body: JSON.stringify(data),
        headers: {
          'Content-Type': 'application/json',
          'Authorization': JSON.stringify(localStorage.getItem("token")).slice(1, -1)
        }}).then (res => res.json()
        ).then (data => { setUser(prevState => {return {...prevState, ...data}}); localStorage.setItem('user', JSON.stringify(user)); setPrivacyMode(false)}
        ).catch (error => {
            console.error(error);
            setErrorAlert(true);
        })
}

        return (
           <BS.Card className="mb-3 mt-3 w-100" >
               <BS.Card.Header className="d-flex align-items-center justify-content-between">
                  <BS.Card.Title className="float-left"><FaAddressCard className="mr-3" /> {user.name}</BS.Card.Title> 
                  { user.avatar_url ? <BS.Image src={`${user.avatar_url}`} style={{width: 50, height: 50}} className="float-right" roundedCircle /> : 
               <BS.Image src={defaultAvatar} style={{width: 50, height: 50}} className="float-right" roundedCircle />}
               </BS.Card.Header>
               <BS.Card.Body>
               { errorAlert ? <BS.Alert variant="danger">Something went wrong, please try logging in and out again.</BS.Alert> : "" }
               { editMode ?  
               <BS.Form onSubmit={handleSubmit(submitEdit)}>
                {/* <BS.Form.Text className='mb-4'>Note: changes here may take a while to be reflected on the bot. This is due to the bot caching data.<br/> Try editing a member after to make the changes show up.</BS.Form.Text> */}
                <BS.Form.Row>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Name:</BS.Form.Label>
               <Controller as={<BS.Form.Control/>} name="name" control={control}  defaultValue={user.name}/>
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Tag:</BS.Form.Label>
                    <Controller as={<BS.Form.Control/>} name="y" control={control}  defaultValue={tag}/>
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Timezone:</BS.Form.Label>
                    <Controller as={<BS.Form.Control/>} name="tz" control={control}  defaultValue={timezone}/>
                    { invalidTimezone ? <BS.Form.Text>Please enter a valid <a href='https://xske.github.io/tz/' rel="noreferrer" target="_blank">timezone</a></BS.Form.Text> : "" }
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Avatar url:</BS.Form.Label> 
                    <Controller as={<BS.Form.Control/>} name="avatar_url" control={control}  defaultValue={avatar}/>
                </BS.Col>
                </BS.Form.Row>
                <BS.Form.Group className="mt-3">
                <BS.Form.Label>Description:</BS.Form.Label>
                <Controller as={<BS.Form.Control maxLength="1000" as="textarea" />} name="description" control={control} defaultValue={editDesc}/>
            </BS.Form.Group>
                <BS.Button variant="light" onClick={() => setEditMode(false)}>Cancel</BS.Button>  <BS.Button variant="primary" type="submit">Submit</BS.Button>
                </BS.Form> :
               <><BS.Row>
                    <BS.Col className="mb-lg-3" xs={12} lg={3}><b>ID:</b> {user.id}</BS.Col>
                    <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Tag:</b> {tag}</BS.Col>
                    <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Timezone:</b> {timezone}</BS.Col>
                    { privacyView ? "" : <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Privacy:</b> <BS.Button variant="light" size="sm" onClick={() => setPrivacyView(true)}>View</BS.Button></BS.Col> }
                </BS.Row>
                { privacyMode ? <BS.Form onSubmit={handleSubmit(submitPrivacy)}>
                <hr/>
                <h5>Editing privacy settings</h5>
                    <BS.Form.Row>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                        <BS.Form.Label>Description:</BS.Form.Label>
                        <BS.Form.Control name="description_privacy" as="select" ref={register}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Member list:</BS.Form.Label>
                        <BS.Form.Control name="member_list_privacy" as="select" ref={register}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Front:</BS.Form.Label>
                        <BS.Form.Control name="front_privacy" as="select" ref={register}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Front history:</BS.Form.Label>
                        <BS.Form.Control name="front_history_privacy" as="select" ref={register}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                </BS.Form.Row>
                <BS.Button variant="light" onClick={() => setPrivacyMode(false)}>Cancel</BS.Button> <BS.Button variant="primary" type="submit">Submit</BS.Button>                
                <hr/>
            </BS.Form> : privacyView ? <><hr/>
                <h5>Viewing privacy settings</h5>
            <BS.Row>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Description:</b> {user.description_privacy}</BS.Col>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Member list: </b>{user.member_list_privacy}</BS.Col>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Front:</b> {user.front_privacy}</BS.Col> 
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Front history:</b> {user.front_history_privacy}</BS.Col>
            </BS.Row>
            <BS.Button variant="light" onClick={() => setPrivacyView(false)}>Exit</BS.Button>  <BS.Button variant="primary" onClick={() => setPrivacyMode(true)}>Edit</BS.Button>
            <hr/></> : "" }
                <p><b>Description:</b></p>
                <p dangerouslySetInnerHTML={{__html: desc}}></p>
                { privacyMode ? "" : privacyView ? "" : <BS.Button variant="light" onClick={() => setEditMode(true)}>Edit</BS.Button>}</> }
                </BS.Card.Body>
           </BS.Card>
        )
}

   

    