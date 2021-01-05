import React, { useEffect, useState } from 'react';
import  * as BS from 'react-bootstrap'
import { useForm } from "react-hook-form";
import moment from 'moment';
import Popup from 'reactjs-popup';
import 'reactjs-popup/dist/index.css';
import autosize from 'autosize';
import LazyLoad from 'react-lazyload';
import Twemoji from 'react-twemoji';

import API_URL from "../Constants/constants.js";

import defaultAvatar from '../default_discord_avatar.png'
import { FaUser, FaTrashAlt } from "react-icons/fa";

export default function MemberCard(props) {

    const [member, setMember] = useState(props.member);

    const [ displayName, setDisplayName ] = useState("");
    const [ birthday, setBirthday ] = useState("");
    const [ birthdate, setBirthdate ] = useState("");
    const [ pronouns, setPronouns ] = useState("");
    const [ editPronouns, setEditPronouns ] = useState("");
    const [ avatar, setAvatar ] = useState("");
    const [ color, setColor ] = useState("");
    const [ desc, setDesc ] = useState("");
    const [ editDesc, setEditDesc ] = useState("");
    const [ proxyTags, setProxyTags ] = useState(member.proxy_tags);

    const [ editMode, setEditMode ] = useState(false);
    const [ privacyMode, setPrivacyMode ] = useState(false);
    const [ privacyView, setPrivacyView ] = useState(false);
    const [ proxyView, setProxyView ] = useState(false);
    const [ proxyMode, setProxyMode ] = useState(false);
    
    const [open, setOpen] = useState(false);
    const closeModal = () => setOpen(false);

    const [ errorAlert, setErrorAlert ] = useState(false);
    const [ wrongID, setWrongID ] = useState(false);
    const [ memberDeleted, setMemberDeleted ] = useState(false);

    const {
        register: registerEdit,
        handleSubmit: handleSubmitEdit
      } = useForm();

    const {
        register: registerPrivacy,
        handleSubmit: handleSubmitPrivacy
      } = useForm();

    const {
        register: registerDelete,
        handleSubmit: handleSubmitDelete
      } = useForm();
  
      const {
        register: registerProxy,
        handleSubmit: handleSubmitProxy,
        } = useForm();

        useEffect(() => {
            autosize(document.querySelector('textarea'));
        })

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
            setPronouns(toHTML(member.pronouns));
            setEditPronouns(member.pronouns);
        } else { setPronouns('');
        setEditPronouns('');
    }

        if (member.avatar_url) {
            var avatarsmall = member.avatar_url.replace('&format=jpeg', '');
            setAvatar(avatarsmall.replace('?width=256&height=256', ''))
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
    }, [member.description, member.color, member.birthday, member.display_name, member.pronouns, member.avatar_url, member.proxy_tags]);

    const submitEdit = data => {
        fetch(`${API_URL}m/${member.id}`,{
            method: 'PATCH',
            body: JSON.stringify(data),
            headers: {
              'Content-Type': 'application/json',
              'Authorization': JSON.stringify(localStorage.getItem("token")).slice(1, -1)
            }}).then (res => res.json()
            ).then (data => { 
                setMember(prevState => {return {...prevState, ...data}});
                setErrorAlert(false);
                setEditMode(false);
        }
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
            ).then (data => {
                setMember(prevState => {return {...prevState, ...data}});
                setErrorAlert(false); 
                setPrivacyMode(false)
        }
            ).catch (error => {
                console.error(error);
                setErrorAlert(true);
             })
    }

    const deleteMember = data => {
        if (data.memberID !== member.id) {
        setWrongID(true);
        } else {
            fetch(`${API_URL}m/${member.id}`,{
                method: 'DELETE',
                headers: {
                'Authorization': JSON.stringify(localStorage.getItem("token")).slice(1, -1)
                }}).then (() => {
                    setErrorAlert(false);
                    setMemberDeleted(true);
                })
                .catch (error => {
                    console.error(error);
                    setErrorAlert(true);
                })
        }
    }

    function addProxyField() {
        setProxyTags(oldTags => [...oldTags, {prefix: '', suffix: ''}] )
    }

    function resetProxyFields() {
        setProxyMode(false);
        setProxyTags(member.proxy_tags);
    }

    const submitProxy = data => {

        const newdata = {proxy_tags: data.proxy_tags.filter(tag => !(tag.prefix === "" && tag.suffix === ""))}

        fetch(`${API_URL}m/${member.id}`,{
            method: 'PATCH',
            body: JSON.stringify(newdata),
            headers: {
              'Content-Type': 'application/json',
              'Authorization': JSON.stringify(localStorage.getItem("token")).slice(1, -1)
            }}).then (res => res.json()
            ).then (data => { 
                setMember(prevState => {return {...prevState, ...data}}); 
                setProxyTags(data.proxy_tags); 
                setErrorAlert(false)
                setProxyMode(false);
        }
            ).catch (error => {
                console.error(error);
                setErrorAlert(true);
            });
    }    

    return (
       memberDeleted ? <BS.Card.Header className="d-flex align-items-center justify-content-between"><BS.Button variant="link" className="float-left"><FaTrashAlt className="mr-4"/>Member Deleted</BS.Button></BS.Card.Header> :
       <LazyLoad offset={100}>
           <BS.Card.Header className="d-flex align-items-center justify-content-between">
        <BS.Accordion.Toggle  as={BS.Button} variant="link" eventKey={member.id} className="float-left"><FaUser className="mr-4 float-left" /> <b>{member.name}</b> ({member.id})</BS.Accordion.Toggle>
            { member.avatar_url ?   <Popup trigger={<BS.Image src={`${member.avatar_url}`} style={{width: 50, height: 50}} tabIndex="0" className="float-right" roundedCircle />} className="avatar" modal>
                {close => (
                    <div className="text-center w-100 m-0" onClick={() => close()}>
                    <BS.Image src={`${avatar}`} style={{'max-width': 640, height: 'auto'}} thumbnail />
                    </div>
                )}
            </Popup> : 
        <BS.Image src={defaultAvatar} style={{width: 50, height: 50}} tabIndex="0" className="float-right" roundedCircle />}
        </BS.Card.Header>
        <BS.Accordion.Collapse eventKey={member.id}>
            <BS.Card.Body style={{borderLeft: `5px solid #${color}` }}>
                { errorAlert ? <BS.Alert variant="danger">Something went wrong, please try logging in and out again.</BS.Alert> : "" }
                { editMode ?
                <>
                <BS.Form id='Edit' onSubmit={handleSubmitEdit(submitEdit)}>
                <BS.Form.Row>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Name:</BS.Form.Label>
                   <BS.Form.Control name="name" ref={registerEdit} defaultValue={member.name} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Display name: </BS.Form.Label>
                    <BS.Form.Control name="display_name" ref={registerEdit}  defaultValue={displayName} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Birthday:</BS.Form.Label>
                    <BS.Form.Control  pattern="^\d{4}\-(0[1-9]|1[012])\-(0[1-9]|[12][0-9]|3[01])$" name="birthday" ref={registerEdit}  defaultValue={birthdate}/>
                    <BS.Form.Text>(YYYY-MM-DD)</BS.Form.Text>
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Pronouns:</BS.Form.Label>
                    <BS.Form.Control maxLength="100" name="pronouns" ref={registerEdit} defaultValue={editPronouns} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Avatar url:</BS.Form.Label> 
                  <BS.Form.Control type="url" name="avatar_url" ref={registerEdit}  defaultValue={avatar} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Color:</BS.Form.Label> 
                   <BS.Form.Control  pattern="[A-Fa-f0-9]{6}" name="color" ref={registerEdit}  defaultValue={color} />
                    <BS.Form.Text>(hexcode)</BS.Form.Text>
                </BS.Col>
            </BS.Form.Row>
            <BS.Form.Group className="mt-3">
                <BS.Form.Label>Description:</BS.Form.Label>
                <BS.Form.Control maxLength="1000" as="textarea" name="description" ref={registerEdit} defaultValue={editDesc}/>
            </BS.Form.Group>
            <BS.Button variant="light" onClick={() => setEditMode(false)}>Cancel</BS.Button> <BS.Button variant="primary" type="submit">Submit</BS.Button> <BS.Button variant="danger" className="float-right" onClick={() => setOpen(o => !o)}>Delete</BS.Button>
            </BS.Form>
                   <Popup open={open} position="top-center" modal>
                       <BS.Container>
                       <BS.Card>
                           <BS.Card.Header>
                               <h5><FaTrashAlt className="mr-3"/> Are you sure you want to delete {member.name}?</h5>
                           </BS.Card.Header>
                           <BS.Card.Body>
                             { wrongID ? <BS.Alert variant="danger">Incorrect ID, please check the spelling.</BS.Alert> : "" }
                               <p>If you're sure you want to delete this member, please enter the member ID ({member.id}) below.</p>
                               <BS.Form id='Delete' onSubmit={handleSubmitDelete(deleteMember)}>
                                   <BS.Form.Label>Member ID:</BS.Form.Label>
                                   <BS.Form.Control className="mb-4" name="memberID" ref={registerDelete({required: true})} placeholder={member.id} />
                                   <BS.Button variant="danger" type="submit">Delete</BS.Button> <BS.Button variant="light" className="float-right" onClick={closeModal}>Cancel</BS.Button>
                               </BS.Form>
                           </BS.Card.Body>
                       </BS.Card>
                       </BS.Container>
                    </Popup></>
                 :
            <>
            <BS.Row>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>ID:</b> {member.id}</BS.Col>
                { member.display_name ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Display name: </b>{displayName}</BS.Col> : "" }
                { member.birthday ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Birthday:</b> {birthday}</BS.Col> : "" }
                { member.pronouns ?  localStorage.getItem('twemoji') ? <BS.Col className="mb-lg-3" xs={12} lg={3}><Twemoji options={{ className: 'twemoji' }}><b>Pronouns:</b> <span dangerouslySetInnerHTML={{__html: pronouns}}></span></Twemoji></BS.Col> : 
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Pronouns:</b><span dangerouslySetInnerHTML={{__html: pronouns}}></span></BS.Col> : "" }
                { member.color ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Color:</b> {color}</BS.Col> : "" }
                { privacyView ? "" : proxyView ? "" : <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Privacy:</b> <BS.Button variant="light" size="sm" onClick={() => setPrivacyView(true)}>View</BS.Button></BS.Col> }
                { privacyView ? "" : proxyView ? "" : <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Proxy tags:</b> <BS.Button variant="light" size="sm" onClick={() => setProxyView(true)}>View</BS.Button></BS.Col> }
                
            </BS.Row>
            { privacyMode ? <BS.Form id='Privacy' onSubmit={handleSubmitPrivacy(submitPrivacy)}>
                <hr/>
                <h5>Editing privacy settings</h5>
                    <BS.Form.Row>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                        <BS.Form.Label>Visibility:</BS.Form.Label>
                        <BS.Form.Control name="visibility" defaultValue={member.visibility} as="select" ref={registerPrivacy}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Name:</BS.Form.Label>
                        <BS.Form.Control name="name_privacy" defaultValue={member.name_privacy} as="select" ref={registerPrivacy}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Description:</BS.Form.Label>
                        <BS.Form.Control name="description_privacy" defaultValue={member.description_privacy} as="select" ref={registerPrivacy}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Avatar:</BS.Form.Label>
                        <BS.Form.Control name="avatar_privacy" defaultValue={member.avatar_privacy} as="select" ref={registerPrivacy}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Birthday:</BS.Form.Label>
                        <BS.Form.Control name="birthday_privacy" defaultValue={member.birthday_privacy} as="select" ref={registerPrivacy}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Pronouns:</BS.Form.Label>
                        <BS.Form.Control name="pronoun_privacy" defaultValue={member.pronoun_privacy} as="select" ref={registerPrivacy}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-3" xs={12} lg={3}>
                    <BS.Form.Label>Meta:</BS.Form.Label>
                        <BS.Form.Control name="metadata_privacy" defaultValue={member.metadata_privacy} as="select" ref={registerPrivacy}>
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
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Avatar:</b> {member.avatar_privacy}</BS.Col>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Birthday:</b> {member.birthday_privacy}</BS.Col>
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Pronouns:</b> {member.pronoun_privacy}</BS.Col>
                <BS.Col className="mb-3" xs={12} lg={3}><b>Meta:</b> {member.metadata_privacy}</BS.Col>
            </BS.Row>
         <BS.Button variant="light" onClick={() => setPrivacyView(false)}>Exit</BS.Button>  <BS.Button variant="primary" onClick={() => setPrivacyMode(true)}>Edit</BS.Button>
         <hr/></> : "" }
         { proxyMode ?
         <><hr/>
         <h5>Editing proxy tags</h5>
         <BS.Form onSubmit={handleSubmitProxy(submitProxy)}>
             <BS.Form.Row>
                { proxyTags.map((item, index) => (
                    <BS.Col key={item.id} className="mb-lg-2" xs={12} lg={2}>
                        <BS.Form.Row>
                        <BS.InputGroup className="ml-1 mr-1 mb-1">
                        <BS.Form.Control name={`proxy_tags[${index}].prefix`} defaultValue={item.prefix} ref={registerProxy}/> 
                        <BS.Form.Control disabled placeholder='text'/>
                        <BS.Form.Control name={`proxy_tags[${index}].suffix`} defaultValue={item.suffix} ref={registerProxy}/>
                        </BS.InputGroup>
                        </BS.Form.Row>
                    </BS.Col>
                ))} <BS.Col className="mb-2" xs={12} lg={2}><BS.Button block variant="light" onClick={() => addProxyField()}>Add new</BS.Button></BS.Col>
             </BS.Form.Row>
             <BS.Button variant="light" onClick={() => resetProxyFields()}>Exit</BS.Button> <BS.Button variant="primary" type="submit">Submit</BS.Button>
        </BS.Form><hr/></> : proxyView ? 
         <><hr/>
             <h5>Viewing proxy tags</h5>
         <BS.Row className="mb-2">
          { proxyTags.length === 0 ? <BS.Col className="mb-lg-2"><b>No proxy tags set.</b></BS.Col> : proxyTags.map((proxytag) => <BS.Col key={proxytag.index} className="mb-lg-2" xs={12} lg={2}> <code>{proxytag.prefix}text{proxytag.suffix}</code></BS.Col> )}
         </BS.Row>
         <BS.Button variant="light" onClick={() => setProxyView(false)}>Exit</BS.Button>  <BS.Button variant="primary" onClick={() => setProxyMode(true)}>Edit</BS.Button>
            <hr/></> : "" }
            <p><b>Description:</b></p>
            { localStorage.getItem('twemoji') ? <Twemoji options={{ className: 'twemoji' }}><p dangerouslySetInnerHTML={{__html: desc}}></p></Twemoji> : <p dangerouslySetInnerHTML={{__html: desc}}></p>}
                { proxyView ? "" : privacyMode ? "" : privacyView ? "" : <BS.Button variant="light" onClick={() => setEditMode(true)}>Edit</BS.Button>}
            </> } </BS.Card.Body>
        </BS.Accordion.Collapse>
        </LazyLoad>
        
    )
}
