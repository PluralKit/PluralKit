import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import  * as BS from 'react-bootstrap'
import { useForm } from "react-hook-form";
import moment from 'moment';
import Popup from 'reactjs-popup';
import 'reactjs-popup/dist/index.css';
import autosize from 'autosize';
import Twemoji from 'react-twemoji';

import API_URL from "../../Constants/constants.js";
import history from "../../History.js";

import defaultAvatar from '../../default_discord_avatar.png'
import { FaLink, FaLock, FaTrashAlt } from "react-icons/fa";

export default function MemberPage(props) {

    const [ member, setMember] = useState(props.member);
    const system = JSON.parse(localStorage.getItem('user'));
    const sysID = system.id;

    const [ displayName, setDisplayName ] = useState("");
    const [ birthday, setBirthday ] = useState("");
    const [ birthdate, setBirthdate ] = useState("");
    const [ created, setCreated ] = useState("");
    const [ pronouns, setPronouns ] = useState("");
    const [ editPronouns, setEditPronouns ] = useState("");
    const [ avatar, setAvatar ] = useState("");
    const [ banner, setBanner ] = useState("");
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
        handleSubmit: handleSubmitEdit,
        setValue
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
            autosize(document.querySelectorAll('textarea'));
        })

    useEffect(() => {
        const { toHTML } = require('../../Functions/discord-parser.js');

        if (member.display_name) {
            setDisplayName(member.display_name)
        } else setDisplayName('')

        if (member.birthday) { 
            setBirthdate(member.birthday)
            if (member.birthday.startsWith('0004-')) {
                var bdaymoment = moment(member.birthday, 'YYYY-MM-DD').format('MMM D');
                setBirthday(bdaymoment);
            } else {
                var birthdaymoment =  moment(member.birthday, 'YYYY-MM-DD').format('MMM D, YYYY');
                setBirthday(birthdaymoment);
            }
        } else { setBirthday('');
        setBirthdate('');
    }

        var createdmoment = moment(member.created).format('MMM D, YYYY');
        setCreated(createdmoment);


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

        if (member.banner) {
            setBanner(member.banner);
          } else setBanner("");
        
        if (member.color) {
            setColor(member.color);
        } else setColor('');

        if (member.description) {
            setDesc(toHTML(member.description));
            setEditDesc(member.description);
        } else { setDesc("(no description)");
        setEditDesc("");
    }
    }, [member.description, member.color, member.birthday, member.display_name, member.pronouns, member.avatar_url, member.proxy_tags, member.created, member.banner]);

    const submitEdit = data => {
        props.edit(Object.assign(member, data));

        fetch(`${API_URL}m/${member.id}`,{
            method: 'PATCH',
            body: JSON.stringify(data),
            headers: {
              'Content-Type': 'application/json',
              'Authorization': localStorage.getItem("token")
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
        props.edit(Object.assign(member, data));
        
        fetch(`${API_URL}m/${member.id}`,{
            method: 'PATCH',
            body: JSON.stringify(data),
            headers: {
              'Content-Type': 'application/json',
              'Authorization': localStorage.getItem("token")
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
                'Authorization': localStorage.getItem("token")
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
        props.edit(Object.assign(member, newdata));

        fetch(`${API_URL}m/${member.id}`,{
            method: 'PATCH',
            body: JSON.stringify(newdata),
            headers: {
              'Content-Type': 'application/json',
              'Authorization': localStorage.getItem("token")
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

    function copyLink() {
        var link = `https://pk-webs.spectralitree.com/profile/${sysID}/${member.id}`
        var textField = document.createElement('textarea')
        textField.innerText = link
        document.body.appendChild(textField);

        textField.select();
        textField.setSelectionRange(0, 99999);
        document.execCommand('copy');

        document.body.removeChild(textField);
    }

    return (
        memberDeleted ? <BS.Card className="mb-5"><BS.Card.Header className="d-flex align-items-center justify-content-between"><BS.Button variant="link" className="float-left"><FaTrashAlt className="mr-4"/>Member Deleted</BS.Button></BS.Card.Header>
        <BS.Card.Body>
            Member successfully deleted, click the button below to go back to the dash.
            <BS.Button variant="primary" className="float-right" onClick={() => history.push("/dash/reload")}>Back</BS.Button>
        </BS.Card.Body></BS.Card> :
        <>
        { member.banner && !localStorage.getItem("hidebanners") ? <div className="banner" style={{backgroundImage: `url(${banner})`}} alt=""/> : ""}
        { localStorage.getItem('colorbg') && member.color ? "" : <><div className="backdrop" style={{backgroundColor: `#${color}`}}/>
        { !localStorage.getItem('fullbg') ? <div className="backdrop-overlay"/> : "" }</> }
        <BS.Card className="mb-5">
        <BS.Card.Header className="d-flex align-items-center justify-content-between">
        <div> { member.visibility === 'public' ? <BS.OverlayTrigger placement="left" overlay={ 
            <BS.Tooltip>
                Copy public link
            </BS.Tooltip>
        }><BS.Button variant="link" onClick={() => copyLink()}><FaLink style={{fontSize: '1.25rem'}}/></BS.Button></BS.OverlayTrigger> : <BS.Button variant="link"><FaLock style={{fontSize: '1.25rem'}} /></BS.Button> }<BS.Button variant="link" ><b>{member.name}</b> ({member.id})</BS.Button> </div> 
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
                { errorAlert ? <BS.Alert variant="danger">Something went wrong, please try logging in and out again.</BS.Alert> : "" }
                { editMode ?
                <>
                <BS.Form id='Edit' onSubmit={handleSubmitEdit(submitEdit)}>
                <BS.Form.Row>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Name:</BS.Form.Label>
                   <BS.Form.Control name="name" {...registerEdit("name")} defaultValue={member.name} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Display name: </BS.Form.Label>
                    <BS.Form.Control name="display_name" {...registerEdit("display_name")}  defaultValue={displayName} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Birthday:</BS.Form.Label>
                    <BS.Form.Control  pattern="^\d{4}\-(0[1-9]|1[012])\-(0[1-9]|[12][0-9]|3[01])$" name="birthday" {...registerEdit("birthday")}  defaultValue={birthdate}/>
                    <BS.Form.Text>(YYYY-MM-DD)</BS.Form.Text>
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Pronouns:</BS.Form.Label>
                    <BS.Form.Control maxLength="100" name="pronouns" {...registerEdit("pronouns")} defaultValue={editPronouns} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Avatar url:</BS.Form.Label> 
                  <BS.Form.Control type="url" name="avatar_url" {...registerEdit("avatar_url")}  defaultValue={avatar} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Banner url:</BS.Form.Label> 
                  <BS.Form.Control type="url" name="banner" {...registerEdit("banner")}  defaultValue={banner} />
                </BS.Col>
                <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Color:</BS.Form.Label> 
                   <BS.Form.Control  pattern="[A-Fa-f0-9]{6}" name="color" {...registerEdit("color")} defaultValue={color} />
                    <BS.Form.Text>(hexcode)</BS.Form.Text>
                </BS.Col>
            </BS.Form.Row>
            <BS.Form.Group className="mt-3">
                <BS.Form.Label>Description:</BS.Form.Label><br/>
                { localStorage.getItem('template1') ? <BS.Button className="mb-2" size="sm" variant="primary" onClick={() => setValue('description', localStorage.getItem('template1'))}>Template 1</BS.Button> : ""} { localStorage.getItem('template2') ? <BS.Button className="mb-2" size="sm" variant="primary" onClick={() => setValue('description', localStorage.getItem('template2'))}>Template 2</BS.Button> : ""} { localStorage.getItem('template3') ? <BS.Button className="mb-2" size="sm" variant="primary" onClick={() => setValue('description', localStorage.getItem('template3'))}>Template 3</BS.Button> : ""}
                <BS.Form.Control maxLength="1000" as="textarea" name="description" {...registerEdit("description")} defaultValue={editDesc}/>
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
                                   <BS.Form.Control className="mb-4" name="memberID" {...registerDelete("memberID", {required: true})} placeholder={member.id} />
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
                { member.display_name ? localStorage.getItem('twemoji') ? <BS.Col className="mb-lg-3" xs={12} lg={3}><Twemoji options={{ className: 'twemoji' }}><b>Display name: </b>{displayName}</Twemoji></BS.Col> :
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Display name:</b> {displayName}</BS.Col> : "" }
                { member.birthday ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Birthday:</b> {birthday}</BS.Col> : "" }
                { member.pronouns ?  localStorage.getItem('twemoji') ? <BS.Col className="mb-lg-3" xs={12} lg={3}><Twemoji options={{ className: 'twemoji' }}><b>Pronouns:</b> <span dangerouslySetInnerHTML={{__html: pronouns}}></span></Twemoji></BS.Col> : 
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Pronouns:</b> <span dangerouslySetInnerHTML={{__html: pronouns}}></span></BS.Col> : "" }
                { member.color ? <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Color:</b> {color}</BS.Col> : "" }
                { privacyView ? "" : proxyView ? "" : <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Privacy:</b> <BS.Button variant="light" size="sm" onClick={() => setPrivacyView(true)}>View</BS.Button></BS.Col> }
                { privacyView ? "" : proxyView ? "" : <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Proxy tags:</b> <BS.Button variant="light" size="sm" onClick={() => setProxyView(true)}>View</BS.Button></BS.Col> }
                <BS.Col className="mb-lg-3" xs={12} lg={3}><b>Created:</b> {created}</BS.Col>
            </BS.Row>
            { privacyMode ? <BS.Form id='Privacy' onSubmit={handleSubmitPrivacy(submitPrivacy)}>
                <hr/>
                <h5>Editing privacy settings</h5>
                <BS.Form.Row>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                        <BS.Form.Label>Visibility:</BS.Form.Label>
                        <BS.Form.Control name="visibility" defaultValue={member.visibility} as="select" {...registerPrivacy("visibility")}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Name:</BS.Form.Label>
                        <BS.Form.Control name="name_privacy" defaultValue={member.name_privacy} as="select" {...registerPrivacy("name_privacy")}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Description:</BS.Form.Label>
                        <BS.Form.Control name="description_privacy" defaultValue={member.description_privacy} as="select" {...registerPrivacy("description_privacy")}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Avatar:</BS.Form.Label>
                        <BS.Form.Control name="avatar_privacy" defaultValue={member.avatar_privacy} as="select" {...registerPrivacy("avatar_privacy")}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Birthday:</BS.Form.Label>
                        <BS.Form.Control name="birthday_privacy" defaultValue={member.birthday_privacy} as="select" {...registerPrivacy("birthday_privacy")}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-lg-2" xs={12} lg={3}>
                    <BS.Form.Label>Pronouns:</BS.Form.Label>
                        <BS.Form.Control name="pronoun_privacy" defaultValue={member.pronoun_privacy} as="select" {...registerPrivacy("pronoun_privacy")}>
                            <option>public</option>
                            <option>private</option>
                        </BS.Form.Control>
                    </BS.Col>
                    <BS.Col className="mb-3" xs={12} lg={3}>
                    <BS.Form.Label>Meta:</BS.Form.Label>
                        <BS.Form.Control name="metadata_privacy" defaultValue={member.metadata_privacy} as="select" {...registerPrivacy("metadata_privacy")}>
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
                    <BS.Col key={index} className="mb-lg-2" xs={12} lg={2}>
                        <BS.Form.Row>
                        <BS.InputGroup className="ml-1 mr-1 mb-1">
                        <BS.Form.Control as="textarea" rows="1" name={`proxy_tags[${index}].prefix`} defaultValue={item.prefix} {...registerProxy(`proxy_tags[${index}].prefix`)}/> 
                        <BS.Form.Control as="textarea" rows="1" disabled placeholder='text'/>
                        <BS.Form.Control as="textarea" rows="1" name={`proxy_tags[${index}].suffix`} defaultValue={item.suffix} {...registerProxy(`proxy_tags[${index}].suffix`)}/>
                        </BS.InputGroup>
                        </BS.Form.Row>
                    </BS.Col>
                ))} <BS.Col className="mb-2" xs={12} lg={3}><BS.Button block variant="light" onClick={() => addProxyField()}>Add new</BS.Button></BS.Col>
             </BS.Form.Row>
             <BS.Button variant="light" onClick={() => resetProxyFields()}>Exit</BS.Button> <BS.Button variant="primary" type="submit">Submit</BS.Button>
        </BS.Form><hr/></> : proxyView ? 
         <><hr/>
             <h5>Viewing proxy tags</h5>
         <BS.Row className="mb-2">
          { proxyTags.length === 0 ? <BS.Col className="mb-lg-2"><b>No proxy tags set.</b></BS.Col> : proxyTags.map((proxytag, index) => <BS.Col key={index} className="mb-lg-2" xs={12} lg={2}> <code>{proxytag.prefix}text{proxytag.suffix}</code></BS.Col> )}
         </BS.Row>
         <BS.Button variant="light" onClick={() => setProxyView(false)}>Exit</BS.Button>  <BS.Button variant="primary" onClick={() => setProxyMode(true)}>Edit</BS.Button>
            <hr/></> : "" }
            <p><b>Description:</b></p>
            { localStorage.getItem('twemoji') ? <Twemoji options={{ className: 'twemoji' }}><p dangerouslySetInnerHTML={{__html: desc}}></p></Twemoji> : <p dangerouslySetInnerHTML={{__html: desc}}></p>}
                { proxyView ? "" : privacyMode ? "" : privacyView ? "" : <><BS.Button variant="light" onClick={() => setEditMode(true)}>Edit</BS.Button> <Link to="/dash" ><BS.Button variant="primary" className="float-right">Back</BS.Button></Link></>}
            </> } </BS.Card.Body></BS.Card></>
    )
}
