import React, { useEffect, useState } from 'react';
import  * as BS from 'react-bootstrap'
import { useForm, Controller } from "react-hook-form";
import autosize from 'autosize';
import moment from 'moment';

import API_URL from "../Constants/constants.js";

import defaultAvatar from '../default_discord_avatar.png'
import { FaUser } from "react-icons/fa";

export default function MemberCard(props) {

    const { register, handleSubmit, control } = useForm();

    const [member, setMember] = useState(props.member);

    const [ displayName, setDisplayName ] = useState("");
    const [ birthday, setBirthday ] = useState("");
    const [ birthdate, setBirthdate ] = useState("");
    const [ pronouns, setPronouns ] = useState("");
    const [ avatar, setAvatar ] = useState("");
    const [ color, setColor ] = useState("");
    const [ desc, setDesc ] = useState("");
    const [ editDesc, setEditDesc ] = useState("");

    const [ editMode, setEditMode ] = useState(false);
    const [ privacyMode, setPrivacyMode ] = useState(false);
    const [ privacyView, setPrivacyView ] = useState(false);

    const [ errorAlert, setErrorAlert ] = useState(false);
  

    useEffect(() => {
        const { toHTML } = require('../Functions/discord-parser.js');

        if (member.display_name) {
            setDisplayName(member.display_name)
        } else setDisplayName('')

        if (member.birthday) { 
            setBirthdate(member.birthday)
            if (member.birthday.startsWith('0004-')) {
                var bday = member.birthday.replace('0004-','');
                var bdaymoment = moment(bday, 'MM-DD').format('MMM D');
                setBirthday(bdaymoment);
            } else {
                var birthdaymoment =  moment(member.birthday, 'YYYY-MM-DD').format('MMM D, YYYY');
                setBirthday(birthdaymoment);
            }
        } else { setBirthday('');
        setBirthdate('');
    }

        if (member.pronouns) {
            setPronouns(member.pronouns)
        } else setPronouns('')

        if (member.avatar_url) {
            setAvatar(member.avatar_url)
        } else setAvatar('')

        if (member.color) {
            setColor(member.color);
        } else setColor('');

        if (member.description) {
            setDesc(toHTML(member.description));
            setEditDesc(member.description);
        } else { setDesc("(no description)");
        setEditDesc("");
    }

    }, [member.description, member.color, member.birthday, member.display_name, member.pronouns, member.avatar_url]);

    useEffect(() => {
        autosize(document.querySelector('textarea'));
    })

    const submitEdit = data => {
        fetch(`${API_URL}m/${member.id}`,{
            method: 'PATCH',
            body: JSON.stringify(data),
            headers: {
              'Content-Type': 'application/json',
              'Authorization': JSON.stringify(localStorage.getItem("token")).slice(1, -1)
            }}).then (res => res.json()
            ).then (data => { setMember(prevState => {return {...prevState, ...data}}); setEditMode(false)}
            ).catch (error => {
                console.error(error);
                setErrorAlert(true);
            });
    }

    const submitPrivacy = data => {
        fetch(`${API_URL}m/${member.id}`,{
            method: 'PATCH',
            body: JSON.stringify(data),
            headers: {
              'Content-Type': 'application/json',
              'Authorization': JSON.stringify(localStorage.getItem("token")).slice(1, -1)
            }}).then (res => res.json()
            ).then (data => { setMember(prevState => {return {...prevState, ...data}}); setPrivacyMode(false)}
            ).catch (error => {
                console.error(error);
                setErrorAlert(true);
             })
    }

    return (
       <>
       <BS.Card.Header className="d-flex align-items-center justify-content-between">
        <BS.Accordion.Toggle  as={BS.Button} variant="link" eventKey={member.id} className="float-left"><FaUser className="mr-4" /> <b>{member.name}</b> ({member.id})</BS.Accordion.Toggle>
            { member.avatar_url ? <BS.Image src={`${member.avatar_url}`} style={{width: 50, height: 50}} className="float-right" roundedCircle /> : 
        <BS.Image src={defaultAvatar} style={{width: 50, height: 50}} className="float-right" roundedCircle />}     
        </BS.Card.Header>
        <BS.Accordion.Collapse eventKey={member.id}>
            <BS.Card.Body style={{borderLeft: `5px solid #${color}` }}>
                { errorAlert ? <BS.Alert variant="danger">Something went wrong, please try logging in and out again.</BS.Alert> : "" }
                { editMode ?
                <BS.Form onSubmit={handleSubmit(submitEdit)}>
                <BS.Form.Row>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Name:</BS.Form.Label>
                    <Controller as={<BS.Form.Control />} name="name" control={control}  defaultValue={member.name} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>AKA: </BS.Form.Label>
                    <Controller as={<BS.Form.Control />} name="display_name" control={control}  defaultValue={displayName} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Birthday:</BS.Form.Label>
                    <Controller as={<BS.Form.Control  pattern="^\d{4}\-(0[1-9]|1[012])\-(0[1-9]|[12][0-9]|3[01])$"/>} name="birthday" control={control}  defaultValue={birthdate}/>
                    <BS.Form.Text>(YYYY-MM-DD)</BS.Form.Text>
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Pronouns:</BS.Form.Label>
                    <Controller as={<BS.Form.Control/>} name="pronouns" control={control}  defaultValue={pronouns} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Avatar url:</BS.Form.Label> 
                    <Controller as={<BS.Form.Control type="url"/>} name="avatar_url" control={control}  defaultValue={avatar} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Color:</BS.Form.Label> 
                    <Controller as={<BS.Form.Control  pattern="[A-Fa-f0-9]{6}"/>} name="color" control={control}  defaultValue={color} />
                    <BS.Form.Text>(hexcode)</BS.Form.Text>
                </BS.Col>
            </BS.Form.Row>
            <BS.Form.Group className="mt-3">
                <BS.Form.Label>Description:</BS.Form.Label>
                <Controller as={<BS.Form.Control maxLength="1000" as="textarea" />} name="description" control={control} defaultValue={editDesc}/>
            </BS.Form.Group>
            <BS.Button variant="light" onClick={() => setEditMode(false)}>Cancel</BS.Button>  <BS.Button variant="primary" type="submit">Submit</BS.Button>
                    </BS.Form>
                 :
            <>
            <BS.Row>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>ID:</b> {member.id}</BS.Col>
                { member.display_name ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>AKA: </b>{displayName}</BS.Col> : "" }
                { member.birthday ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Birthday:</b> {birthday}</BS.Col> : "" }
                { member.pronouns ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Pronouns:</b> {pronouns}</BS.Col> : "" }
                { member.color ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Color:</b> {color}</BS.Col> : "" }
                { privacyView ? "" : <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Privacy:</b> <BS.Button variant="light" size="sm" onClick={() => setPrivacyView(true)}>View</BS.Button></BS.Col> }
                
            </BS.Row>
            { privacyMode ? <BS.Form onSubmit={handleSubmit(submitPrivacy)}>
                <hr/>
                <h5>Editing privacy settings</h5>
                    <BS.Form.Row>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                        <BS.Form.Label>Visibility:</BS.Form.Label>
                        <BS.Form.Control name="visibility" as="select" ref={register}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Name:</BS.Form.Label>
                        <BS.Form.Control name="name_privacy" as="select" ref={register}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Description:</BS.Form.Label>
                        <BS.Form.Control name="description_privacy" as="select" ref={register}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Birthday:</BS.Form.Label>
                        <BS.Form.Control name="birthday_privacy" as="select" ref={register}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Pronouns:</BS.Form.Label>
                        <BS.Form.Control name="pronoun_privacy" as="select" ref={register}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-3" xs={12} lg={3}>
                    <BS.Form.Label>Meta:</BS.Form.Label>
                        <BS.Form.Control name="metadata_privacy" as="select" ref={register}>
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
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Visibility:</b> {member.visibility}</BS.Col>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Name: </b>{member.name_privacy}</BS.Col>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Description:</b> {member.description_privacy}</BS.Col> 
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Birthday:</b> {member.birthday_privacy}</BS.Col>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Pronouns:</b> {member.pronoun_privacy}</BS.Col>
                <BS.Col className="mb-3" xs={12} lg={3}><b>Meta:</b> {member.metadata_privacy}</BS.Col>
            </BS.Row>
            <BS.Button variant="light" onClick={() => setPrivacyView(false)}>Exit</BS.Button>  <BS.Button variant="primary" onClick={() => setPrivacyMode(true)}>Edit</BS.Button>
            <hr/></> : "" }
            <p><b>Description:</b></p>
            <p dangerouslySetInnerHTML={{__html: desc}}></p>
                { privacyMode ? "" : privacyView ? "" : <BS.Button variant="light" onClick={() => setEditMode(true)}>Edit</BS.Button>}
            </> } </BS.Card.Body>
        </BS.Accordion.Collapse>
        </>
    )
}
