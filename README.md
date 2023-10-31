# Hello, Bedrock Images!

This is the code project for the [Hello, Bedrock Images!](https://davidpallmann.hashnode.dev/hello-bedrock-images) blog post. 

This episode: Amazon Bedrock and Generative AI Images. In this Hello, Cloud blog series, we're covering the basics of AWS cloud services for newcomers who are .NET developers. If you love C# but are new to AWS, or to this particular service, this should give you a jumpstart.

In this post we'll explore Amazon Bedrock and generative AI image processing. For an introduction to Bedrock, see the Hello Bedrock! post. We'll use Bedrock today to generate images based on text prompts using a Lambda function. We'll do this step-by-step, making no assumptions other than familiarity with C# and Visual Studio. We're using Visual Studio 2022 and .NET 6.

## Our Hello, Bedrock Images Project

We will first get familiar with Bedrock in the AWS console using the Bedrock image playground. Then we'll write a .NET AWS Lambda function that generates images in response to text prompts, using S3. You'll provide your prompt by uploading a text file to S3, and the Lambda function will generate a corresponding .png file with a matching image.

See the blog post for the tutorial to create this project and run it on AWS.

