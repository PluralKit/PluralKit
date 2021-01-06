import React, { useState, useEffect } from 'react';
import * as BS from 'react-bootstrap';
import { useParams } from "react-router-dom";
import MemberPage from './MemberPage.js'

export default function MemberPages(props) {
    const { memberID } = useParams();
    const [ noMatch, setNoMatch ] = useState(false);
    
    const memberpages = props.members.filter((member) => member.id === memberID)
    const memberpage = memberpages.map((member) => <MemberPage key={member.id} member={member}/>)

    useEffect (() => { 
        if (memberpages.length === 0) {
            setNoMatch(true);
         }
    }, [memberpages])

    if (noMatch) return (
        <BS.Alert variant="danger">You do not have a member with the ID '{memberID}' in your system. Please check the ID again.</BS.Alert>
    )

    return (
        <>
            {memberpage}
        </>
    )
}