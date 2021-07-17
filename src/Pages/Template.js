import { useEffect, useState } from 'react';
import * as BS from 'react-bootstrap';
import { FaFileAlt } from 'react-icons/fa';
import { useForm } from 'react-hook-form';
import autosize from 'autosize';

const Template = () => {
    const [isSubmit, setIsSubmit] = useState(false);

    const { register, handleSubmit } = useForm();
    
    var template1 = "";
    var template2 = "";
    var template3 = "";
    if (localStorage.getItem('template1') !== null) template1 = localStorage.getItem('template1');
    if (localStorage.getItem('template2') !== null) template2 = localStorage.getItem('template2');
    if (localStorage.getItem('template3') !== null) template3 = localStorage.getItem('template3');

    const onSubmit = data => {
        localStorage.setItem('template1', data.template1);
        localStorage.setItem('template2', data.template2);
        localStorage.setItem('template3', data.template3);
        setIsSubmit(true);
    };

    useEffect(() => {
        autosize(document.querySelectorAll('textarea'));
    });

    return (
        <>
        <BS.Card className="mb-3">
            <BS.Card.Header>
                <BS.Card.Title>
                <FaFileAlt className="mr-3" />
                Templates
                </BS.Card.Title>
            </BS.Card.Header>
            <BS.Card.Body>
                <p>Templates allow you to quickly set up a member description with a specific layout. Put in the template in one of the below fields, and access it whenever you create or edit a member. You can set up to 3 templates.</p>
                <p><b>Note:</b> just like the settings, these templates are saved in your localstorage, which means you will have to set them again on every different device/browser you use. If you clear your local storage, the templates will also be cleared!</p>
            </BS.Card.Body>
        </BS.Card>
        
        <BS.Card>
            <BS.Card.Body>
                <BS.Form onSubmit={handleSubmit(onSubmit)}>
                    <BS.Form.Group className="mt-3">
                        <BS.Form.Label><b>Template 1.</b></BS.Form.Label>
                        <BS.Form.Control maxLength="1000" as="textarea" name="template1" {...register("template1")} defaultValue={template1}/>
                    </BS.Form.Group>
                    <BS.Form.Group className="mt-3">
                        <BS.Form.Label><b>Template 2.</b></BS.Form.Label>
                        <BS.Form.Control maxLength="1000" as="textarea" name="template2" {...register("template2")} defaultValue={template2}/>
                    </BS.Form.Group>
                    <BS.Form.Group className="mt-3">
                        <BS.Form.Label><b>Template 3.</b></BS.Form.Label>
                        <BS.Form.Control maxLength="1000" as="textarea" name="template3" {...register("template3")} defaultValue={template3}/>
                    </BS.Form.Group>
                    <BS.Button variant="primary" className="float-left mr-2" type="submit">Submit</BS.Button> { isSubmit? <p style={{opacity: 0.7}}>Templates saved!</p> : ""}
                </BS.Form>
            </BS.Card.Body>
        </BS.Card>
    </>
    );
}

export default Template;